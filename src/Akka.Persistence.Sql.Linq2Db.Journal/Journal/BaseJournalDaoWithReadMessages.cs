using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Pattern;
using Akka.Streams;
using Akka.Streams.Dsl;
using LanguageExt;
using LinqToDB.Data;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class WriteFinished
    {
        public WriteFinished(string persistenceId, Task future)
        {
            PersistenceId = persistenceId;
            Future = future;
        }
        public string PersistenceId { get; protected set; }
        public Task Future { get; protected set; }
    }
    
    
    
    public abstract class BaseJournalDaoWithReadMessages : IJournalDaoWithReadMessages
    {
        protected readonly AkkaPersistenceDataConnectionFactory _connectionFactory;
        protected BaseJournalDaoWithReadMessages(IAdvancedScheduler ec,
            IMaterializer mat, AkkaPersistenceDataConnectionFactory connectionFactory)
        {
            this.ec = ec;
            this.mat = mat;
            _connectionFactory = connectionFactory;
        }
        protected IAdvancedScheduler ec;
        protected IMaterializer mat;

        public abstract Source<Util.Try<(IPersistentRepresentation, long)>, NotUsed> Messages(DataConnection db, string persistenceId, long fromSequenceNr, long toSequenceNr,
            long max);

        public abstract
            Source<Util.Try<Linq2DbWriteJournal.ReplayCompletion>, NotUsed>
            MessagesClass(DataConnection db, string persistenceId,
                long fromSequenceNr, long toSequenceNr,
                long max);
        public Source<Util.Try<Linq2DbWriteJournal.ReplayCompletion>, NotUsed> MessagesWithBatchClass(string persistenceId, long fromSequenceNr,
            long toSequenceNr, int batchSize, Util.Option<(TimeSpan,SchedulerBase)> refreshInterval)
        {
            var src = Source
                .UnfoldAsync<(long, FlowControl),
                    IEnumerable<Util.Try<Linq2DbWriteJournal.ReplayCompletion>>>(
                    (Math.Max(1, fromSequenceNr),
                        FlowControl.Continue.Instance),
                    async opt =>
                    {
                        async Task<Util.Option<((long, FlowControl), IEnumerable<Util.Try<Linq2DbWriteJournal.ReplayCompletion>>)>>
                            RetrieveNextBatch()
                        {
                            IImmutableList<Util.Try<Linq2DbWriteJournal.ReplayCompletion>> msg;
                            using (var conn =
                                _connectionFactory.GetConnection())
                            {
                                msg =
                                    await MessagesClass(conn, persistenceId, opt.Item1,
                                            toSequenceNr, batchSize)
                                        .RunWith(
                                            Sink.Seq<Util.Try<Linq2DbWriteJournal.ReplayCompletion>>(), mat);
                            }

                            var hasMoreEvents = msg.Count == batchSize;
                            var lastMsg = msg.LastOrDefault();
                            Util.Option<long> lastSeq = Util.Option<long>.None;
                            if (lastMsg != null && lastMsg.IsSuccess)
                            {
                                lastSeq = lastMsg.Success.Select(r => r.SequenceNr);
                            }
                            else if (lastMsg != null &&  lastMsg.Failure.HasValue)
                            {
                                throw lastMsg.Failure.Value;
                            }

                            var hasLastEvent =
                                lastSeq.HasValue &&
                                lastSeq.Value >= toSequenceNr;
                            FlowControl nextControl = null;
                            if (hasLastEvent || opt.Item1 > toSequenceNr)
                            {
                                nextControl = FlowControl.Stop.Instance;
                            }
                            else if (hasMoreEvents)
                            {
                                nextControl = FlowControl.Continue.Instance;
                            }
                            else if (refreshInterval.HasValue == false)
                            {
                                nextControl = FlowControl.Stop.Instance;
                            }
                            else
                            {
                                nextControl = FlowControl.ContinueDelayed
                                    .Instance;
                            }

                            long nextFrom = 0;
                            if (lastSeq.HasValue)
                            {
                                nextFrom = lastSeq.Value + 1;
                            }
                            else
                            {
                                nextFrom = opt.Item1;
                            }

                            return new Util.Option<((long, FlowControl), IEnumerable<Util.Try<Linq2DbWriteJournal.ReplayCompletion>>)>((
                                    (nextFrom, nextControl), msg));
                        }

                        if (opt.Item2 is FlowControl.Stop)
                        {
                            return Util.Option<((long, FlowControl), IEnumerable<Util.Try<Linq2DbWriteJournal.ReplayCompletion>>)>.None;
                        }
                        else if (opt.Item2 is FlowControl.Continue)
                        {
                            return await RetrieveNextBatch();
                        }
                        else if (opt.Item2 is FlowControl.ContinueDelayed)
                        {
                            if (refreshInterval.HasValue)
                            {
                                return await FutureTimeoutSupport.After(refreshInterval.Value.Item1,refreshInterval.Value.Item2, RetrieveNextBatch);
                                
                            }
                        }

                        throw null;
                    });

            return src.SelectMany(r => r);
        }

        
        
        public Source<Util.Try<(IPersistentRepresentation, long)>, NotUsed> MessagesWithBatch(string persistenceId, long fromSequenceNr,
            long toSequenceNr, int batchSize, Util.Option<(TimeSpan,SchedulerBase)> refreshInterval)
        {
            var src = Source
                .UnfoldAsync<(long, FlowControl),
                    Seq<Util.Try<(IPersistentRepresentation, long)>>>(
                    (Math.Max(1, fromSequenceNr),
                        FlowControl.Continue.Instance),
                    async opt =>
                    {
                        async Task<Util.Option<((long, FlowControl), Seq<Util.Try<(IPersistentRepresentation, long)>>)>>
                            RetrieveNextBatch()
                        {
                            Seq<
                                Util.Try<(IPersistentRepresentation, long)>> msg;
                            using (var conn =
                                _connectionFactory.GetConnection())
                            {
                                msg =
                                    await Messages(conn, persistenceId, opt.Item1,
                                            toSequenceNr, batchSize)
                                        .RunWith(
                                            ExtSeq.Seq<Util.Try<(
                                                IPersistentRepresentation,
                                                long)>>(), mat);
                            }

                            var hasMoreEvents = msg.Count == batchSize;
                            var lastMsg = msg.LastOrDefault();
                            Util.Option<long> lastSeq = Util.Option<long>.None;
                            if (lastMsg != null && lastMsg.IsSuccess)
                            {
                                lastSeq = lastMsg.Success.Select(r => r.Item1.SequenceNr);
                            }
                            else if (lastMsg != null &&  lastMsg.Failure.HasValue)
                            {
                                throw lastMsg.Failure.Value;
                            }

                            var hasLastEvent =
                                lastSeq.HasValue &&
                                lastSeq.Value >= toSequenceNr;
                            FlowControl nextControl = null;
                            if (hasLastEvent || opt.Item1 > toSequenceNr)
                            {
                                nextControl = FlowControl.Stop.Instance;
                            }
                            else if (hasMoreEvents)
                            {
                                nextControl = FlowControl.Continue.Instance;
                            }
                            else if (refreshInterval.HasValue == false)
                            {
                                nextControl = FlowControl.Stop.Instance;
                            }
                            else
                            {
                                nextControl = FlowControl.ContinueDelayed
                                    .Instance;
                            }

                            long nextFrom = 0;
                            if (lastSeq.HasValue)
                            {
                                nextFrom = lastSeq.Value + 1;
                            }
                            else
                            {
                                nextFrom = opt.Item1;
                            }

                            return new Util.Option<((long, FlowControl), Seq<Util.Try<(IPersistentRepresentation, long)>>)>((
                                    (nextFrom, nextControl), msg));
                        }

                        switch (opt.Item2)
                        {
                            case FlowControl.Stop _:
                                return Util.Option<((long, FlowControl), Seq<Util.Try<(IPersistentRepresentation, long)>>)>.None;
                            case FlowControl.Continue _:
                                return await RetrieveNextBatch();
                            case FlowControl.ContinueDelayed _ when refreshInterval.HasValue:
                                return await FutureTimeoutSupport.After(refreshInterval.Value.Item1,refreshInterval.Value.Item2, RetrieveNextBatch);
                            default:
                                throw null;
                        }
                    });

            return src.SelectMany(r => r);
        }
    }
}