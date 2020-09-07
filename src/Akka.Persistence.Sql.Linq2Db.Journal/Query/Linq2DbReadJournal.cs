using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Linq2Db.Journal.Config;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util.Internal;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Query
{
    public class MissingElements
    {
        public IImmutableList<Range>
    }

    public class AssumeMaxOrderingIdTimerKey
    {
        public static AssumeMaxOrderingIdTimerKey Instance => new AssumeMaxOrderingIdTimerKey();
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
            return receive(message, 0, new Dictionary<int, MissingElements>(), 0, queryDelay);
        }

        protected bool receive(object message, long currentMaxOrdering,
            Dictionary<int, MissingElements> missingByCounter,
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
        }

        private void findGaps(IImmutableList<long> elements,
            long currentMaxOrdering,
            Dictionary<int, MissingElements> missingByCounter, int moduloCounter)
        {
            var givenUp =
                missingByCounter.GetOrElse(moduloCounter,
                    new MissingElements());
            elements.Aggregate((currentMaxOrdering, currentMaxOrdering, missin))
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
        public object Elements { get; set; }
        
        public NewOrderingIds(in long currentMaxOrdering, object res)
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
        ICurrentEventsByTagQuery,
        IAllEventsQuery,
        ICurrentAllEventsQuery
    {
        private IActorRef journalSequenceActor;
        public Linq2DbReadJournal(ExtendedActorSystem system, Config config)
        {
            system.ActorOf()
        }

        
    }
}