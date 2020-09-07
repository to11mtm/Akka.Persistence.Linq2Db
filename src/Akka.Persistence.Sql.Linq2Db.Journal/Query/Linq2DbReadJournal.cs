using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence.Journal;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Linq2Db.Journal.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util.Internal;
using LanguageExt;
using LinqToDB;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Query
{
    public class NumericRangeEntry
    {
        public NumericRangeEntry(long from, long until)
        {
            this.from = from;
            this.until = until;
        }
        public long from { get; set; }
        public long until { get; set; }

        public bool InRange(long number)
        {
            return  from <= number && number <= until;
        }

        public IEnumerable<long> ToEnumerable()
        {
            var itemCount = until - from;
            List<long> returnList;
            if (itemCount < Int32.MaxValue)
            {
                returnList = new List<long>();
            }
            else
            {
                returnList = new List<long>();
            }
            
            for (long i = from; i < until; i++)
            {
               returnList.Add(i);
            }

            return returnList;
        }
    }
    public class MissingElements
    {
        public MissingElements(Seq<NumericRangeEntry> elements)
        {
            Elements = elements;
        }

        public MissingElements AddRange(long from, long until)
        {
            return new MissingElements(
                Elements.Add(new NumericRangeEntry(from, until)));
        }

        public bool Contains(long id)
        {
            return Elements.Any(r => r.InRange(id));
        }

        public bool Isempty => Elements.IsEmpty;
        public Seq<NumericRangeEntry> Elements { get; protected set; }
        
        public static readonly MissingElements Empty = new MissingElements(Seq<NumericRangeEntry>.Empty);
    }

    public class AssumeMaxOrderingIdTimerKey
    {
        public static AssumeMaxOrderingIdTimerKey Instance => new AssumeMaxOrderingIdTimerKey();
    }
    public class QueryOrderingIdsTimerKey
    {
        public static QueryOrderingIdsTimerKey Instance => new QueryOrderingIdsTimerKey();
    }
    public class JournalSequenceActor : ActorBase, IWithTimers
    {
        private JournalSequenceRetrievalConfig _config;
        private IReadJournalDAO _readJournalDao;
        private TimeSpan queryDelay;
        private int maxTries;
        private ILoggingAdapter _log;
        private ActorMaterializer _mat;
        public ITimerScheduler Timers { get; set; }
        public JournalSequenceActor(IReadJournalDAO readJournalDao,
            JournalSequenceRetrievalConfig config)
        {
            _mat = ActorMaterializer.Create(Context,
                ActorMaterializerSettings.Create(Context.System),
                "linq2db-query");
            _readJournalDao = readJournalDao;
            _config = config;
            queryDelay = config.QueryDelay;
            maxTries = config.MaxTries;
            _log = Context.GetLogger();
        }

        protected bool receive(object message)
        {
            return receive(message, 0, ImmutableDictionary<int, MissingElements>.Empty, 0, queryDelay);
        }

        protected bool receive(object message, long currentMaxOrdering,
            IImmutableDictionary<int, MissingElements> missingByCounter,
            int moduloCounter, TimeSpan previousDelay)
        {
            if (message is ScheduleAssumeMaxOrderingId s)
            {
                var delay = queryDelay * maxTries;
                Timers.StartSingleTimer(AssumeMaxOrderingIdTimerKey.Instance, new AssumeMaxOrderingId(s.MaxInDatabase),delay);
            }
            else if (message is AssumeMaxOrderingId a)
            {
                if (currentMaxOrdering < a.Max)
                {
                    Become((o => receive(o, maxTries, missingByCounter,
                        moduloCounter, previousDelay)));
                }
            }
            else if (message is GetMaxOrderingId)
            {
                Sender.Tell(new MaxOrderingId(currentMaxOrdering));
            }
            else if (message is QueryOrderingIds)
            {
                _readJournalDao
                    .journalSequence(currentMaxOrdering, _config.BatchSize)
                    .RunWith(Sink.Seq<long>(), _mat).PipeTo(Self, sender: Self,
                        success: res =>
                            new NewOrderingIds(currentMaxOrdering, res));
            }
            else if (message is NewOrderingIds nids)
            {
                if (nids.MaxOrdering < currentMaxOrdering)
                {
                    Self.Tell(new QueryOrderingIds());
                }
                else
                {
                    findGaps(nids.Elements, currentMaxOrdering,
                        missingByCounter, moduloCounter);
                }
            }
            else if (message is Status.Failure t)
            {
                var newDelay =
                    _config.MaxBackoffQueryDelay.Min(previousDelay * 2);
                if (newDelay == _config.MaxBackoffQueryDelay)
                {
                    _log.Warning(
                        "Failed to query max Ordering ID Because of {0}, retrying in {1}",
                        t, newDelay);
                }

                scheduleQuery(newDelay);
                Context.Become(o => receive(o, currentMaxOrdering,
                    missingByCounter, moduloCounter, newDelay));
            }
            else
            {
                return false;
            }

            return true;
        }

        private void findGaps(IImmutableList<long> elements,
            long currentMaxOrdering,
            IImmutableDictionary<int, MissingElements> missingByCounter, int moduloCounter)
        {
            var givenUp =
                missingByCounter.ContainsKey(moduloCounter) ? missingByCounter[moduloCounter]:
                    MissingElements.Empty;
            var (nextmax,_,missingElems) = elements.Aggregate(
                (currentMax: currentMaxOrdering,
                    previousElement: currentMaxOrdering,
                    missing: MissingElements.Empty),
                (agg, currentElement) =>
                {
                    long newMax = 0;

                    if (new NumericRangeEntry(agg.Item1 + 1, currentElement)
                        .ToEnumerable().ForAll(p => givenUp.Contains(p)))
                    {
                        newMax = currentElement;
                    }
                    else
                    {
                        newMax = agg.currentMax;
                    }

                    MissingElements newMissing;
                    if (agg.previousElement + 1 == currentElement ||
                        newMax == currentElement)
                    {
                        newMissing = agg.missing;
                    }
                    else
                    {
                        newMissing = agg.missing.AddRange(agg.Item2 + 1,
                            currentElement);
                    }

                    return (newMax, currentElement, newMissing);
                });
            var newMissingByCounter =
                missingByCounter.SetItem(moduloCounter, missingElems);
            var noGapsFound = missingElems.Isempty;
            var isFullBatch = elements.Count == _config.BatchSize;
            if (noGapsFound && isFullBatch)
            {
                Self.Tell(new QueryOrderingIds());
                Context.Become(o=> receive(o,nextmax,newMissingByCounter,moduloCounter, queryDelay));
            }
            else
            {
                scheduleQuery(queryDelay);
                Context.Become(o => receive(o, nextmax, newMissingByCounter,
                    (moduloCounter + 1) % _config.MaxTries, queryDelay));
            }

        }

        public void scheduleQuery(TimeSpan delay)
        {
            Timers.StartSingleTimer(QueryOrderingIdsTimerKey.Instance,
                new QueryOrderingIds(), delay);
        }

        protected override bool Receive(object message)
        {
            throw new NotImplementedException();
        }

        protected override void PreStart()
        {
            var self = Self;
            self.Tell(new QueryOrderingIds());
            _readJournalDao.maxJournalSequence().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _log.Info(
                        "Failed to recover fast, using event-by-event recovery instead",
                        t.Exception);
                }
                else if (t.IsCompleted)
                {
                    self.Tell(new ScheduleAssumeMaxOrderingId(t.Result));
                }
            });
            base.PreStart();
        }

        
    }

    public class NewOrderingIds
    {
        public long MaxOrdering { get; }
        public IImmutableList<long> Elements { get; set; }
        
        public NewOrderingIds(long currentMaxOrdering, IImmutableList<long> res)
        {
            MaxOrdering = currentMaxOrdering;
            Elements = res;
        }
    }

    public class AssumeMaxOrderingId
    {
        public AssumeMaxOrderingId(long max)
        {
            Max = max;
        }

        public long Max { get; set; }
    }

    public class GetMaxOrderingId
    {
        
    }
    public class MaxOrderingId
    {
        public MaxOrderingId(long max)
        {
            Max = max;
        }

        public long Max { get; set; }
    }
    public class ScheduleAssumeMaxOrderingId
    {
        public ScheduleAssumeMaxOrderingId(long maxInDatabase)
        {
            MaxInDatabase = maxInDatabase;
        }

        public long MaxInDatabase { get; set; }
    }

    public class QueryOrderingIds
    {
        
    }
    public class Linq2DbReadJournal :  
        IPersistenceIdsQuery,
        ICurrentPersistenceIdsQuery,
        IEventsByPersistenceIdQuery,
        ICurrentEventsByPersistenceIdQuery,
        IEventsByTagQuery,
        ICurrentEventsByTagQuery
    {
        private IActorRef journalSequenceActor;
        private ActorMaterializer _mat;
        private Source<long, ICancelable> delaySource;
        private ByteArrayReadJournalDao readJournalDao;
        private string writePluginId;
        private EventAdapters eventAdapters;
        private ReadJournalConfig readJournalConfig;
        private ExtendedActorSystem system;

        public Linq2DbReadJournal(ExtendedActorSystem system, Configuration.Config config)
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
                ActorMaterializerSettings.Create(system), "l2db-query-mat");
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
                >((from: offset, control: FlowControl.Continue.Instance),
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