using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Journal;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Linq2Db.Journal.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Streams;
using Akka.Streams.Dsl;
using LanguageExt;
using LinqToDB;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Query
{
    public class Linq2DbReadJournal :  
        IPersistenceIdsQuery,
        ICurrentPersistenceIdsQuery,
        IEventsByPersistenceIdQuery,
        ICurrentEventsByPersistenceIdQuery,
        IEventsByTagQuery,
        ICurrentEventsByTagQuery
    {
        public static Configuration.Config DefaultConfiguration =>
            ConfigurationFactory.FromResource<Linq2DbReadJournal>(
                "Akka.Persistence.Sql.Linq2Db.Journal.Query.reference.conf");
        private IActorRef journalSequenceActor;
        private ActorMaterializer _mat;
        private Source<long, ICancelable> delaySource;
        private ByteArrayReadJournalDao readJournalDao;
        private string writePluginId;
        private EventAdapters eventAdapters;
        private ReadJournalConfig readJournalConfig;
        private ExtendedActorSystem system;

        public Linq2DbReadJournal(ExtendedActorSystem system, Configuration.Config config, string configPath)
        {
            this.system = system;
            writePluginId = config.GetString("write-plugin");
            eventAdapters = Persistence.Instance.Get(system)
                .AdaptersFor(writePluginId);
            readJournalConfig = new ReadJournalConfig(config);
            var connFact =new AkkaPersistenceDataConnectionFactory(readJournalConfig);
            readJournalDao = new ByteArrayReadJournalDao(
                system.Scheduler.Advanced, _mat,
                connFact, readJournalConfig,
                new ByteArrayJournalSerializer(readJournalConfig,
                    system.Serialization,
                    readJournalConfig.PluginConfig.TagSeparator));
            _mat = ActorMaterializer.Create(system,
                ActorMaterializerSettings.Create(system), "l2db-query-mat"+configPath);
            journalSequenceActor= system.ActorOf(Props.Create(() => new JournalSequenceActor(readJournalDao
                    ,
                    readJournalConfig.JournalSequenceRetrievalConfiguration)),
                readJournalConfig.TableConfig.TableName +
                "akka-persistence-linq2db-sequence-actor");
            delaySource = Source.Tick(readJournalConfig.RefreshInterval,
                TimeSpan.FromSeconds(0), 0L).Take(1);
        }

        public Source<string, NotUsed> CurrentPersistenceIds()
        {
            return readJournalDao.allPersistenceIdsSource(long.MaxValue);
        }

        public Source<string, NotUsed> PersistenceIds()
        {
            return Source.Repeat(0L)
                .ConcatMany<long, string, NotUsed>(_ =>

                    delaySource.MapMaterializedValue(r => NotUsed.Instance)
                        .ConcatMany<long, string, NotUsed>(_ =>
                            CurrentPersistenceIds())
                ).StatefulSelectMany<string,string,NotUsed>(()=> 
                {
                    var knownIds = ImmutableHashSet<string>.Empty;

                    IEnumerable<string> next(string id)
                    {
                        var xs =
                            ImmutableHashSet<string>.Empty.Add(id).Except(knownIds);
                        knownIds = knownIds.Add(id);
                        return xs;
                    }

                    return (id)=> next(id);
                });
        }

        private IImmutableList<IPersistentRepresentation> adaptEvents(
            IPersistentRepresentation persistentRepresentation)
        {
            var adapter =
                eventAdapters.Get(persistentRepresentation.Payload.GetType());
            return adapter
                .FromJournal(persistentRepresentation.Payload,
                    persistentRepresentation.Manifest).Events.Select(e =>
                    persistentRepresentation.WithPayload(e)).ToImmutableList();
        }

        public Source<EventEnvelope, NotUsed> CurrentEventsByPersistenceId(string persistenceId, long fromSequenceNr,
            long toSequenceNr)
        {
            return eventsByPersistenceIdSource(persistenceId, fromSequenceNr,
                toSequenceNr, Util.Option<(TimeSpan, SchedulerBase)>.None);
        }

        public Source<EventEnvelope, NotUsed> EventsByPersistenceId(string persistenceId, long fromSequenceNr,
            long toSequenceNr)
        {
            return eventsByPersistenceIdSource(persistenceId, fromSequenceNr,
                toSequenceNr, Util.Option<(TimeSpan, SchedulerBase)>.None);
        }

        public Source<EventEnvelope, NotUsed> eventsByPersistenceIdSource(
            string persistenceId, long fromSequenceNr, long toSequenceNr,
            Akka.Util.Option<(TimeSpan, SchedulerBase)> refreshInterval)
        {
            var batchSize = readJournalConfig.MaxBufferSize;
            return readJournalDao.MessagesWithBatch(persistenceId, fromSequenceNr,
                    toSequenceNr, batchSize, refreshInterval)
                .SelectAsync(1,
                    reprAndOrdNr => Task.FromResult(reprAndOrdNr.Get()))
                .SelectMany((ReplayCompletion r) => adaptEvents(r.repr)
                    .Select(p => new {repr = r.repr, ordNr = r.SequenceNr}))
                .Select(r => new EventEnvelope(new Sequence(r.ordNr),
                    r.repr.PersistenceId, r.repr.SequenceNr, r.repr.Payload));
        }

        public Source<EventEnvelope, NotUsed> CurrentEventsByTag(string tag, Offset offset)
        {
            return currentEventsByTag(tag, (offset as Sequence)?.Value??0);
        }

        private Source<EventEnvelope, NotUsed> currentJournalEventsByTag(
            string tag, long offset, long max, MaxOrderingId latestOrdering)
        {
            if (latestOrdering.Max < offset)
            {
                return Source.Empty<EventEnvelope>();
            }

            return readJournalDao
                .eventsByTag(tag, offset, latestOrdering.Max, max).SelectAsync(
                    1,
                    r =>
                        Task.FromResult(r.Get())
                ).SelectMany<(IPersistentRepresentation,
                    IImmutableSet<string>, long),EventEnvelope,NotUsed>(
                    (a) 
                        =>
                    
                {
                    return adaptEvents(a.Item1).Select(r =>
                        new EventEnvelope(new Sequence(a.Item3),
                            r.PersistenceId,
                            r.SequenceNr, r.Payload));
                });
        }

        private Source<EventEnvelope, NotUsed> eventsByTag(string tag,
            long offset, long? terminateAfterOffset)
        {
            var askTimeout = readJournalConfig
                .JournalSequenceRetrievalConfiguration.AskTimeout;
            var batchSize = readJournalConfig.MaxBufferSize;
            return Source
                .UnfoldAsync<(long, FlowControl), IImmutableList<EventEnvelope>
                >((offset, FlowControl.Continue.Instance),
                    uf =>
                    {
                        async Task<Util.Option<((long, FlowControl),
                            IImmutableList<EventEnvelope>)>> retrieveNextBatch()
                        {
                            var queryUntil =
                                await journalSequenceActor.Ask<MaxOrderingId>(
                                    new GetMaxOrderingId(), askTimeout);
                            var xs =
                                await currentJournalEventsByTag(tag, uf.Item1,
                                    batchSize,
                                    queryUntil).RunWith(
                                    Sink.Seq<EventEnvelope>(),
                                    _mat);
                            var hasMoreEvents = xs.Count == batchSize;
                            FlowControl nextControl = null;
                            if (terminateAfterOffset.HasValue)
                            {
                                if (!hasMoreEvents &&
                                    terminateAfterOffset.Value <=
                                    queryUntil.Max)
                                    nextControl = FlowControl.Stop.Instance;
                                if (xs.Exists(r =>
                                    (r.Offset is Sequence s) &&
                                    s.Value >= terminateAfterOffset.Value))
                                    nextControl = FlowControl.Stop.Instance;
                            }

                            if (nextControl == null)
                            {
                                nextControl = hasMoreEvents
                                    ? (FlowControl) FlowControl.Continue
                                        .Instance
                                    : FlowControl.ContinueDelayed.Instance;
                            }

                            var nextStartingOffset = (xs.Count == 0)
                                ? Math.Max(uf.Item1, queryUntil.Max)
                                : xs.Select(r => r.Offset as Sequence)
                                    .Where(r => r != null).Max(t => t.Value);
                            return new
                                Util.Option<((long nextStartingOffset,
                                    FlowControl
                                    nextControl), IImmutableList<EventEnvelope>
                                    xs)
                                >((
                                    (nextStartingOffset, nextControl), xs));
                        }

                        switch (uf.Item2)
                        {
                            case FlowControl.Stop _:
                                return
                                    Task.FromResult(Util
                                        .Option<((long, FlowControl),
                                            IImmutableList<EventEnvelope>)>
                                        .None);
                            case FlowControl.Continue _:
                                return retrieveNextBatch();
                            case FlowControl.ContinueDelayed _:
                                return Akka.Pattern.FutureTimeoutSupport.After(
                                    readJournalConfig.RefreshInterval,
                                    system.Scheduler,
                                    retrieveNextBatch);
                            default:
                                return
                                    Task.FromResult(Util
                                        .Option<((long, FlowControl),
                                            IImmutableList<EventEnvelope>)>
                                        .None);
                        }
                    }).SelectMany(r => r);
        }

        private Source<EventEnvelope, NotUsed> currentEventsByTag(string tag,
            long offset)
        {
            return Source.FromTask(readJournalDao.maxJournalSequence()).ConcatMany(
                maxInDb =>
                {
                    return eventsByTag(tag, offset, Some.Create(maxInDb));
                });
        }

        public Source<EventEnvelope, NotUsed> EventsByTag(string tag, Offset offset)
        {
            long theOffset = 0;
            if (offset is Sequence s)
            {
                theOffset = s.Value;
            }

            return eventsByTag(tag, theOffset, null);
        }
    }
}