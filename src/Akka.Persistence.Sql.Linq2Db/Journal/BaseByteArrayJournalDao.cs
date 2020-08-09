using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Serialization;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using LinqToDB;
using LinqToDB.Data;

namespace Akka.Persistence.Sql.Linq2Db
{
    public abstract class BaseByteArrayJournalDao : BaseJournalDaoWithReadMessages,
        IJournalDaoWithUpdates
    {
        protected BaseByteArrayJournalDao(IAdvancedScheduler sched, IMaterializer materializerr,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            JournalConfig config, ByteArrayJournalSerializer serializer):base(sched,materializerr)
        {
            _connectionFactory = connectionFactory;
            _journalConfig = config;
            Serializer = serializer;
            WriteQueue = Source
                .Queue<(TaskCompletionSource<NotUsed>, List<JournalRow>)
                >(_journalConfig.DaoConfig.BufferSize,
                    OverflowStrategy.DropNew).BatchWeighted(_journalConfig.DaoConfig.BatchSize,
                    cf => cf.Item2.Count, r => (new[] {r.Item1}, r.Item2),
                    ((oldRows, newRows) =>
                    {
                        return (oldRows.Item1.Append(newRows.Item1).ToArray(),
                            oldRows.Item2.Concat(newRows.Item2).ToList());

                    })).SelectAsync(_journalConfig.DaoConfig.Parallelism, async (promisesAndRows) =>
                {
                    try
                    {
                        await writeJournalRows(promisesAndRows.Item2);
                        foreach (var taskCompletionSource in promisesAndRows
                            .Item1)
                        {
                            taskCompletionSource.TrySetResult(NotUsed.Instance);
                        }
                    }
                    catch (Exception e)
                    {
                        foreach (var taskCompletionSource in promisesAndRows
                            .Item1)
                        {
                            taskCompletionSource.TrySetException(e);
                        }
                    }

                    return promisesAndRows;
                }).ToMaterialized(
                    Sink.Ignore<(TaskCompletionSource<NotUsed>[],
                        List<JournalRow>)>(),Keep.Left).Run(mat);
        }
        protected JournalConfig _journalConfig;
        protected FlowPersistentReprSerializer<JournalRow> Serializer;

        private async Task<NotUsed> queueWriteJournalRows(IEnumerable<JournalRow> xs)
        {
            TaskCompletionSource<NotUsed> promise = new TaskCompletionSource<NotUsed>();
            await WriteQueue.OfferAsync((promise, xs.ToList())).ContinueWith(
                 (Task<IQueueOfferResult> task) =>
            {
                var result = task.Result;
                if (result is QueueOfferResult.Enqueued)
                {
                    
                }
                else if (result is QueueOfferResult.Failure f)
                {
                    promise.SetException(new Exception("Failed to write journal row batch",f.Cause));
                }
                else if (result is QueueOfferResult.Dropped)
                {
                    promise.SetException(new Exception(
                        $"Failed to enqueue journal row batch write, the queue buffer was full ({_journalConfig.DaoConfig.BufferSize} elements)"));
                }
                else if (result is QueueOfferResult.QueueClosed)
                {
                    promise.SetException(new Exception("Failed to enqueue journal row batch write, the queue was closed."));
                }
            });
            return await promise.Task;
        }
        private async Task writeJournalRows(List<JournalRow> xs)
        {
            using (var db = _connectionFactory.GetConnection())
            {
                if (xs.Count > 1)
                {
                    db.GetTable<JournalRow>()
                        .TableName(_journalConfig.TableConfiguration.TableName)
                        .BulkCopy(
                            new BulkCopyOptions()
                                {BulkCopyType = BulkCopyType.Default}, xs);
                }
                else if (xs.Count>0) 
                {
                    db.Insert(xs.FirstOrDefault());
                }
            }
            // Write atomically without auto-commit
        }

        public Task<IEnumerable<Try<NotUsed>>> AsyncWriteMessages(
            IEnumerable<AtomicWrite> messages)
        {
            var serializedTries = Serializer.Serialize(messages);
            
            var taskList = serializedTries.Select(async r =>
            {
                if (r.IsSuccess == false)
                {
                    return new Try<NotUsed>(r.Failure.Value);
                }
                else
                {
                    try
                    {
                        await queueWriteJournalRows(r.Get());
                        return new Try<NotUsed>(NotUsed.Instance);
                    }
                    catch (Exception e)
                    {
                        return new Try<NotUsed>(e);
                    }
                }
            }).ToArray();
            var returnFactory = Task<IEnumerable<Try<NotUsed>>>.Factory.ContinueWhenAll(taskList,
                (Task<Try<NotUsed>>[] task) =>
                {
                    return task.Select(t => t.Result);
                });
            return returnFactory;
        }

        private Lazy<object> logWarnAboutLogicalDeletionDeprecation =
            new Lazy<object>(()=>
            {
                return new object();    
            },LazyThreadSafetyMode.None);

        public bool logicalDelete;

        public async Task Delete(string persistenceId, long maxSequenceNr)
        {
            if (logicalDelete)
            {
                var obj = logWarnAboutLogicalDeletionDeprecation.Value;
            }
            else
            {
                using (var db = _connectionFactory.GetConnection())
                {
                    await db.GetTable<JournalRow>().TableName(_journalConfig
                            .TableConfiguration.TableName)
                        .Where(r =>
                            r.persistenceId == persistenceId &&
                            (r.sequenceNumber <= maxSequenceNr))
                        .Set(r => r.deleted, true)
                        .UpdateAsync();
                    if (logicalDelete == false)
                    {
                        await db.GetTable<JournalRow>().TableName(_journalConfig
                                .TableConfiguration.TableName)
                            .Where(r =>
                                r.persistenceId == persistenceId &&
                                (r.sequenceNumber <= maxSequenceNr &&
                                 r.sequenceNumber <
                                 MaxMarkedForDeletionMaxPersistenceIdQuery(
                                         persistenceId)
                                     .FirstOrDefault())).DeleteAsync();
                    }

                }
            }
        }

        protected IQueryable<long> MaxMarkedForDeletionMaxPersistenceIdQuery(string persistenceId)
        {
            using (var db = _connectionFactory.GetConnection())
                return db.GetTable<JournalRow>()
                    .Where(r => r.persistenceId == persistenceId && r.deleted)
                    .OrderByDescending(r => r.sequenceNumber)
                    .Select(r => r.sequenceNumber).Take(1);
        }
        
        private IQueryable<long> MaxSeqNumberForPersistenceIdQuery(DataConnection db, string persistenceId, long minSequenceNumber = 0)
        {
            
                var queryable = db.GetTable<JournalRow>()
                    .Where(r => r.persistenceId == persistenceId);
                if (minSequenceNumber != 0)
                {
                    queryable = queryable.Where(r =>
                        r.sequenceNumber > minSequenceNumber);
                }

                return queryable.OrderByDescending(r => r.sequenceNumber)
                    .Select(r => r.sequenceNumber).Take(1);
        }

        public async Task<Done> Update(string persistenceId, long sequenceNr,
            object payload)
        {
            var write = new Persistent(payload, sequenceNr, persistenceId);
            var serialize = Serializer.Serialize(write);
            if (serialize.IsSuccess)
            {
                throw new ArgumentException($"Failed to serialize {write.GetType()} for update of {persistenceId}] @ {sequenceNr}",serialize.Failure.Value);
            }

            using (var db = _connectionFactory.GetConnection())
            {
                await db.GetTable<JournalRow>()
                    .TableName(_journalConfig.TableConfiguration.TableName)
                    .Where(r =>
                        r.persistenceId == persistenceId &&
                        r.sequenceNumber == write.SequenceNr)
                    .Set(r => r.message, serialize.Get().message)
                    .UpdateAsync();
                return Done.Instance;
            }
        }

        public async Task<long> HighestSequenceNr(string persistenceId,
            long fromSequenceNr)
        {
            using (var db = _connectionFactory.GetConnection())
            {
                return await MaxSeqNumberForPersistenceIdQuery(db,persistenceId,
                    fromSequenceNr).FirstOrDefaultAsync();
            }
        }

        public override Source<Try<(IPersistentRepresentation, long)>, NotUsed> Messages(string persistenceId, long fromSequenceNr, long toSequenceNr,
            long max)
        {
            using (var db = _connectionFactory.GetConnection())
            {
                IQueryable<JournalRow> query = db.GetTable<JournalRow>()
                    .TableName(_journalConfig.TableConfiguration.TableName)
                    .Where(r =>
                        r.persistenceId == persistenceId &&
                        r.sequenceNumber >= fromSequenceNr &&
                        r.sequenceNumber <= toSequenceNr)
                    .OrderBy(r => r.sequenceNumber);
                if (max <= int.MaxValue)
                {
                    query = query.Take((int) max);
                }


                //return Source
                //    .FromObservable(query.AsAsyncEnumerable().ToObservable())
                return Source.From(query.ToList())
                    .Via(
                        Serializer.deserializeFlow()).Select(sertry =>
                    {
                        if (sertry.IsSuccess)
                        {
                            return new Try<(IPersistentRepresentation, long)>((
                                sertry.Success.Value.Item1,
                                sertry.Success.Value.Item3));
                        }
                        else
                        {
                            return new Try<(IPersistentRepresentation, long)>(
                                sertry.Failure.Value);
                        }
                    });
            }
        }

        public ISourceQueueWithComplete<(TaskCompletionSource<NotUsed>, List<JournalRow>)> WriteQueue;
        protected readonly AkkaPersistenceDataConnectionFactory _connectionFactory;
    }
}