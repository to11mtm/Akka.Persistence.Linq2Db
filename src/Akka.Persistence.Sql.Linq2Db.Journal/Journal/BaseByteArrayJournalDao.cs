using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Serialization;
using Akka.Streams;
using Akka.Streams.Dsl;
using LanguageExt;
using LinqToDB;
using LinqToDB.Data;
using static LanguageExt.Prelude;
using Seq = LanguageExt.Seq;

namespace Akka.Persistence.Sql.Linq2Db
{
    public abstract class BaseByteArrayJournalDao :
        BaseJournalDaoWithReadMessages,
        IJournalDaoWithUpdates
    {

        public ISourceQueueWithComplete<WriteQueueEntry> WriteQueue;
        private bool useCompatibleDelete;
        protected JournalConfig _journalConfig;
        protected FlowPersistentReprSerializer<JournalRow> Serializer;

        private Lazy<object> logWarnAboutLogicalDeletionDeprecation =
            new Lazy<object>(() => { return new object(); },
                LazyThreadSafetyMode.None);

        public bool logicalDelete;

        protected BaseByteArrayJournalDao(IAdvancedScheduler sched,
            IMaterializer materializerr,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            JournalConfig config, ByteArrayJournalSerializer serializer) : base(
            sched, materializerr, connectionFactory)
        {
            _journalConfig = config;
            logicalDelete = _journalConfig.DaoConfig.LogicalDelete;
            useCompatibleDelete =
                _journalConfig.DaoConfig.DeleteCompatibilityMode;
            Serializer = serializer;
            WriteQueue = Source
                .Queue<WriteQueueEntry
                >(_journalConfig.DaoConfig.BufferSize,
                    OverflowStrategy.DropNew).BatchWeighted(
                    _journalConfig.DaoConfig.BatchSize,
                    cf => cf.Rows.Count,
                    r => new WriteQueueSet(
                        new List<TaskCompletionSource<NotUsed>>(new[]
                            {r.TCS}), r.Rows),
                    (oldRows, newRows) =>
                    {
                        oldRows.TCS.Add(newRows.TCS);
                        oldRows.Rows = oldRows.Rows.Concat(newRows.Rows);
                        return oldRows; //.Concat(newRows.Item2).ToList());

                    }).SelectAsync(_journalConfig.DaoConfig.Parallelism,
                    async (promisesAndRows) =>
                    {
                        try
                        {
                            await writeJournalRowsSeq(promisesAndRows.Rows);
                            foreach (var taskCompletionSource in promisesAndRows
                                .TCS)
                            {
                                taskCompletionSource.TrySetResult(
                                    NotUsed.Instance);
                            }
                        }
                        catch (Exception e)
                        {
                            foreach (var taskCompletionSource in promisesAndRows
                                .TCS)
                            {
                                taskCompletionSource.TrySetException(e);
                            }
                        }

                        return NotUsed.Instance;
                    }).ToMaterialized(
                    Sink.Ignore<NotUsed>(), Keep.Left).Run(mat);
        }



        private async Task<NotUsed> queueWriteJournalRows(Seq<JournalRow> xs)
        {
            TaskCompletionSource<NotUsed> promise =
                new TaskCompletionSource<NotUsed>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                    );
            var result =
                await WriteQueue.OfferAsync(new WriteQueueEntry(promise, xs));
            {
                if (result is QueueOfferResult.Enqueued)
                {

                }
                else if (result is QueueOfferResult.Failure f)
                {
                    promise.TrySetException(
                        new Exception("Failed to write journal row batch",
                            f.Cause));
                }
                else if (result is QueueOfferResult.Dropped)
                {
                    promise.TrySetException(new Exception(
                        $"Failed to enqueue journal row batch write, the queue buffer was full ({_journalConfig.DaoConfig.BufferSize} elements)"));
                }
                else if (result is QueueOfferResult.QueueClosed)
                {
                    promise.TrySetException(new Exception(
                        "Failed to enqueue journal row batch write, the queue was closed."));
                }

                return await promise.Task;
            }
        }

        private async Task writeJournalRowsSeq(Seq<JournalRow> xs)
        {
            using (var db = _connectionFactory.GetConnection())
            {
                if (xs.Count > 1)
                {
                    //Interlocked.Increment(ref bulkCopyCount);
                    //Interlocked.Add(ref bulkCopyRowCount, xs.Count);
                    await db.GetTable<JournalRow>()
                        .BulkCopyAsync(
                            new BulkCopyOptions()
                            {
                                BulkCopyType =
                                    xs.Count > _journalConfig.DaoConfig
                                        .MaxRowByRowSize
                                        ? BulkCopyType.Default
                                        : BulkCopyType.MultipleRows,
                                UseInternalTransaction = true 
                            }, xs);
                }
                else if (xs.Count > 0)
                {
                    //Interlocked.Increment(ref normalWriteCount);
                    await db.InsertAsync(xs.Head);
                }
            }

            // Write atomically without auto-commit
        }

        public async Task<IImmutableList<Exception>> AsyncWriteMessages(
            IEnumerable<AtomicWrite> messages)
        {

            var serializedTries = Serializer.Serialize(messages);

            var rows = Seq(serializedTries.SelectMany(serializedTry =>
            {
                if (serializedTry.IsSuccess)
                {
                    return serializedTry.Get();
                }

                return new List<JournalRow>(0);
            }).ToList());

            return await queueWriteJournalRows(rows).ContinueWith(task =>
                {
                    return serializedTries.Select(r =>
                        r.IsSuccess
                            ? (task.IsFaulted
                                ? TryUnwrapException(task.Exception)
                                : null)
                            : null).ToImmutableList();
                }, CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        protected static Exception TryUnwrapException(Exception e)
        {
            var aggregateException = e as AggregateException;
            if (aggregateException != null)
            {
                aggregateException = aggregateException.Flatten();
                if (aggregateException.InnerExceptions.Count == 1)
                    return aggregateException.InnerExceptions[0];
            }

            return e;
        }



        public async Task Delete(string persistenceId, long maxSequenceNr)
        {
            if (logicalDelete)
            {
                var obj = logWarnAboutLogicalDeletionDeprecation.Value;
            }
            
            {
                using (var db = _connectionFactory.GetConnection())
                {
                    var transaction =await db.BeginTransactionAsync();
                    try
                    {
                        await db.GetTable<JournalRow>()
                            .Where(r =>
                                r.persistenceId == persistenceId &&
                                (r.sequenceNumber <= maxSequenceNr))
                            .Set(r => r.deleted, true)
                            .UpdateAsync();
                        if (_journalConfig.DaoConfig.DeleteCompatibilityMode)
                        {
                            await db.GetTable<JournalMetaData>()
                                .InsertAsync(() => new JournalMetaData()
                                {
                                    PersistenceId = persistenceId,
                                    SequenceNumber =
                                        MaxMarkedForDeletionMaxPersistenceIdQuery(
                                            persistenceId).FirstOrDefault()
                                });
                        }

                        if (logicalDelete == false)
                        {
                            await db.GetTable<JournalRow>()
                                .Where(r =>
                                    r.persistenceId == persistenceId &&
                                    (r.sequenceNumber <= maxSequenceNr &&
                                     r.sequenceNumber <
                                     MaxMarkedForDeletionMaxPersistenceIdQuery(
                                             persistenceId)
                                         .FirstOrDefault())).DeleteAsync();
                        }

                        if (_journalConfig.DaoConfig.DeleteCompatibilityMode)
                        {
                            await db.GetTable<JournalMetaData>()
                                .Where(r =>
                                    r.PersistenceId == persistenceId &&
                                    r.SequenceNumber <
                                    MaxMarkedForDeletionMaxPersistenceIdQuery(
                                        persistenceId).FirstOrDefault())
                                .DeleteAsync();
                        }

                        await transaction.CommitAsync();
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        protected IQueryable<long> MaxMarkedForDeletionMaxPersistenceIdQuery(
            string persistenceId)
        {
            using (var db = _connectionFactory.GetConnection())
                return db.GetTable<JournalRow>()
                    .Where(r => r.persistenceId == persistenceId && r.deleted)
                    .OrderByDescending(r => r.sequenceNumber)
                    .Select(r => r.sequenceNumber).Take(1);
        }

        private IQueryable<long> MaxSeqNumberForPersistenceIdQuery(
            DataConnection db, string persistenceId, long minSequenceNumber = 0)
        {

            var queryable = db.GetTable<JournalRow>()
                .Where(r => r.persistenceId == persistenceId).Select(r =>
                    new
                    {
                        SequenceNumber = r.sequenceNumber,
                        PersistenceId = r.persistenceId
                    });
            if (minSequenceNumber != 0)
            {
                queryable = queryable.Where(r =>
                    r.SequenceNumber > minSequenceNumber);
            }

            if (_journalConfig.DaoConfig.DeleteCompatibilityMode)
            {
                queryable = queryable.Union(db.GetTable<JournalMetaData>()
                    .Where(r =>
                        r.SequenceNumber > minSequenceNumber &&
                        r.PersistenceId == persistenceId).Select(md =>
                        new
                        {
                            SequenceNumber = md.SequenceNumber,
                            PersistenceId = md.PersistenceId
                        }));
            }

            return queryable.OrderByDescending(r => r.SequenceNumber)
                .Select(r => r.SequenceNumber).Take(1);
        }

        public async Task<Done> Update(string persistenceId, long sequenceNr,
            object payload)
        {
            var write = new Persistent(payload, sequenceNr, persistenceId);
            var serialize = Serializer.Serialize(write);
            if (serialize.IsSuccess)
            {
                throw new ArgumentException(
                    $"Failed to serialize {write.GetType()} for update of {persistenceId}] @ {sequenceNr}",
                    serialize.Failure.Value);
            }

            using (var db = _connectionFactory.GetConnection())
            {
                await db.GetTable<JournalRow>()
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
                return await MaxSeqNumberForPersistenceIdQuery(db,
                    persistenceId,
                    fromSequenceNr).FirstOrDefaultAsync();
            }
        }

        public override
            Source<Util.Try<Linq2DbWriteJournal.ReplayCompletion>, NotUsed>
            MessagesClass(DataConnection db, string persistenceId,
                long fromSequenceNr, long toSequenceNr,
                long max)
        {

            {
                IQueryable<JournalRow> query = db.GetTable<JournalRow>()
                    .Where(r =>
                        r.persistenceId == persistenceId &&
                        r.sequenceNumber >= fromSequenceNr &&
                        r.sequenceNumber <= toSequenceNr &&
                        r.deleted == false)
                    .OrderBy(r => r.sequenceNumber);
                if (max <= int.MaxValue)
                {
                    query = query.Take((int) max);
                }

                
                //TODO: Is there a better way to do this async?
                //return Source
                //    .FromObservable(query.AsAsyncEnumerable().ToObservable())
                
                
                return Source.From(query.ToList())
                    .Via(
                        Serializer.deserializeFlow()).Select(sertry =>
                    {
                        if (sertry.IsSuccess)
                        {
                            return new
                                Util.Try<Linq2DbWriteJournal.ReplayCompletion>(
                                    new Linq2DbWriteJournal.ReplayCompletion()
                                    {
                                        repr = sertry.Success.Value.Item1,
                                        SequenceNr = sertry.Success.Value.Item3
                                    });
                        }
                        else
                        {
                            return new
                                Util.Try<Linq2DbWriteJournal.ReplayCompletion>(
                                    sertry.Failure.Value);
                        }
                    });
            }
        }

        public override
            Source<Util.Try<(IPersistentRepresentation, long)>, NotUsed>
            Messages(DataConnection db, string persistenceId,
                long fromSequenceNr, long toSequenceNr,
                long max)
        {

            {
                IQueryable<JournalRow> query = db.GetTable<JournalRow>()
                    .Where(r =>
                        r.persistenceId == persistenceId &&
                        r.sequenceNumber >= fromSequenceNr &&
                        r.sequenceNumber <= toSequenceNr && r.deleted==false)
                    .OrderBy(r => r.sequenceNumber);
                if (max <= int.MaxValue)
                {
                    query = query.Take((int) max);
                }

                //Source.From(query)
                //query.ToListAsync()
                //    .FromObservable(query.AsAsyncEnumerable().ToObservable())

                return Source.From(query)
                    .Via(
                        Serializer.deserializeFlow()).Select(sertry =>
                    {
                        if (sertry.IsSuccess)
                        {
                            return new
                                Util.Try<(IPersistentRepresentation, long)>((
                                    sertry.Success.Value.Item1,
                                    sertry.Success.Value.Item3));
                        }
                        else
                        {
                            return new
                                Util.Try<(IPersistentRepresentation, long)>(
                                    sertry.Failure.Value);
                        }
                    });
            }
        }
    }
}