using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Dispatch;
using Akka.Pattern;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;

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
        protected BaseJournalDaoWithReadMessages(IAdvancedScheduler ec,
            IMaterializer mat)
        {
            this.ec = ec;
            this.mat = mat;
        }
        protected IAdvancedScheduler ec;
        protected IMaterializer mat;

        public abstract Source<Try<(IPersistentRepresentation, long)>, NotUsed> Messages(string persistenceId, long fromSequenceNr, long toSequenceNr,
            long max);

        public Source<Try<(IPersistentRepresentation, long)>, NotUsed> MessagesWithBatch(string persistenceId, long fromSequenceNr,
            long toSequenceNr, int batchSize, Option<(TimeSpan,SchedulerBase)> refreshInterval)
        {
            var src = Source
                .UnfoldAsync<(long, FlowControl),
                    IEnumerable<Try<(IPersistentRepresentation, long)>>>(
                    (Math.Max(1, fromSequenceNr),
                        FlowControl.Continue.Instance),
                    async opt =>
                    {
                        async Task<Option<((long, FlowControl), IEnumerable<
                                Try<(IPersistentRepresentation, long)>>)>>
                            RetrieveNextBatch()
                        {
                            var msg =
                                await Messages(persistenceId, opt.Item1,
                                        toSequenceNr, batchSize)
                                    .RunWith(
                                        Sink.Seq<Try<(IPersistentRepresentation,
                                            long)>>(), mat);
                            var hasMoreEvents = msg.Count == batchSize;
                            var lastMsg = msg.LastOrDefault();
                            Option<long> lastSeq = Option<long>.None;
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

                            return new
                                Option<((long, FlowControl), IEnumerable<
                                    Try<(IPersistentRepresentation, long)>>)>((
                                    (nextFrom, nextControl), msg));
                        }

                        if (opt.Item2 is FlowControl.Stop)
                        {
                            return Option<((long, FlowControl), IEnumerable<Try<(IPersistentRepresentation, long)>>)>.None;
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
    }
}