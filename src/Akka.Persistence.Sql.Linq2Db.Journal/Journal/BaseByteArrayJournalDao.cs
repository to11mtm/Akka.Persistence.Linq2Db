using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    public class WriteQueueEntry
    {
        public WriteQueueEntry(TaskCompletionSource<NotUsed> tcs,
            List<JournalRow> rows)
        {
            TCS = tcs;
            Rows = rows;
        }

        public List<JournalRow> Rows { get; }

        public TaskCompletionSource<NotUsed> TCS { get; }
    }

    public class WriteQueueSet
    {
        public WriteQueueSet(List<TaskCompletionSource<NotUsed>> tcs,
            List<JournalRow> rows)
        {
            TCS = tcs;
            Rows = rows;
        }

        public List<JournalRow> Rows { get; }

        public List<TaskCompletionSource<NotUsed>> TCS { get; }
    }
    public abstract class BaseByteArrayJournalDao : BaseJournalDaoWithReadMessages,
        IJournalDaoWithUpdates
    {
        protected BaseByteArrayJournalDao(IAdvancedScheduler sched,
            IMaterializer materializerr,
            AkkaPersistenceDataConnectionFactory connectionFactory,
            JournalConfig config, ByteArrayJournalSerializer serializer) : base(
            sched, materializerr, connectionFactory)
        {
            _journalConfig = config;
            Serializer = serializer;
            WriteQueue = Source
                .Queue<WriteQueueEntry
                >(_journalConfig.DaoConfig.BufferSize,
                    OverflowStrategy.DropNew).BatchWeighted(
                    _journalConfig.DaoConfig.BatchSize,
                    cf => cf.Rows.Count,
                    r => new WriteQueueSet (
                        new List<TaskCompletionSource<NotUsed>>(new[]
                            {r.TCS}), r.Rows),
                    (oldRows, newRows) =>
                    {
                        oldRows.TCS.Add(newRows.TCS);
                        oldRows.Rows.AddRange(newRows.Rows);
                        return oldRows; //.Concat(newRows.Item2).ToList());

                    }).SelectAsync(_journalConfig.DaoConfig.Parallelism,
                    async (promisesAndRows) =>
                    {
                        try
                        {
                            await writeJournalRows(promisesAndRows.Rows);
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

                        return promisesAndRows;
                    }).ToMaterialized(
                    Sink.Ignore<WriteQueueSet>(), Keep.Left).Run(mat);
        }

        protected JournalConfig _journalConfig;
        protected FlowPersistentReprSerializer<JournalRow> Serializer;

        private  Task<NotUsed> queueWriteJournalRows(List<JournalRow> xs)
        {
            TaskCompletionSource<NotUsed> promise =
                new TaskCompletionSource<NotUsed>(TaskCreationOptions.RunContinuationsAsynchronously);
            WriteQueue.OfferAsync(new WriteQueueEntry(promise, xs))
                .ContinueWith(
                 async (Task<IQueueOfferResult> task) =>
            {
                var result = await task;
                if (result is QueueOfferResult.Enqueued)
                {
                    
                }
                else if (result is QueueOfferResult.Failure f)
                {
                    promise.TrySetException(new Exception("Failed to write journal row batch",f.Cause));
                }
                else if (result is QueueOfferResult.Dropped)
                {
                    promise.TrySetException(new Exception(
                        $"Failed to enqueue journal row batch write, the queue buffer was full ({_journalConfig.DaoConfig.BufferSize} elements)"));
                }
                else if (result is QueueOfferResult.QueueClosed)
                {
                    promise.TrySetException(new Exception("Failed to enqueue journal row batch write, the queue was closed."));
                }
            });
            return promise.Task;
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
                    await db.InsertAsync(xs.FirstOrDefault());
                }
            }
            // Write atomically without auto-commit
        }

        public Task<IImmutableList<Exception>> AsyncWriteMessagesUnwrapped(
            IEnumerable<AtomicWrite> messages)
        {
            var serializedTries = Serializer.Serialize(messages);
            
            var taskList = serializedTries.Select(async r =>
            {
                if (r.IsSuccess == false)
                {
                    return r.Failure.Value;
                }
                else
                {
                    try
                    {
                        await queueWriteJournalRows(r.Get());
                        return (Exception)null;
                    }
                    catch (Exception e)
                    {
                        return e;
                    }
                }
            }).ToArray();
            var returnFactory =
                Task<IImmutableList<Exception>>.Factory.ContinueWhenAll(
                    taskList,
                (Task<Exception>[] task) =>
                {
                    return task.Select(t => t.Result).ToImmutableList();
                });
            return returnFactory;
        }
/*
    private static Seq resultWhenWriteComplete$1(final Seq serializedTries$1) {
      return (Seq)(serializedTries$1.forall((x$8) -> {
         return BoxesRunTime.boxToBoolean($anonfun$asyncWriteMessages$4(x$8));
      }) ? scala.collection.immutable.Nil..MODULE$ : (Seq)serializedTries$1.map((x$9) -> {
         return x$9.map((x$10) -> {
            $anonfun$asyncWriteMessages$6(x$10);
            return BoxedUnit.UNIT;
         });
      }, scala.collection.immutable.Seq..MODULE$.canBuildFrom()));
   }
 */
        public Task<IImmutableList<Exception>> AsyncWriteMessagesFuture(
            IEnumerable<AtomicWrite> messages)
        {
            var serializedTries = Serializer.Serialize(messages);
            var rows = serializedTries.SelectMany(serializedTry =>
            {
                if (serializedTry.IsSuccess)
                {
                    return serializedTry.Get();
                }

                return new List<JournalRow>(0);
            }).ToList();
            
            return queueWriteJournalRows(rows).ContinueWith(task =>
            {
                return serializedTries.Select(r =>
                    r.IsSuccess
                        ? (task.IsFaulted
                            ? TryUnwrapException(task.Exception)
                            : null)
                        : null).ToImmutableList() as IImmutableList<Exception>;
            });
        }
        public async Task<IImmutableList<Exception>> AsyncWriteMessagesDirect(
            IEnumerable<AtomicWrite> messages)
        {
            var serializedTries = Serializer.Serialize(messages);
            var rows = serializedTries.SelectMany(serializedTry =>
            {
                if (serializedTry.IsSuccess)
                {
                    return serializedTry.Get();
                }

                return new List<JournalRow>(0);
            }).ToList();
            
            try
            {
                await queueWriteJournalRows(rows);
                return serializedTries
                    .Select(r =>
                        r.IsSuccess == false
                            ? r.Failure.Value
                            : null)
                    .ToImmutableList();
            }
            catch (Exception e)
            {
                return serializedTries.Select(r =>e).ToImmutableList();
            }
            //return await queueWriteJournalRows(rows).ContinueWith(task =>
            //{
            //    return serializedTries.Select(r =>
            //        r.IsSuccess
            //            ? (task.IsFaulted
            //                ? TryUnwrapException(task.Exception)
            //                : null)
            //            : null).ToImmutableList();
            //});
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

        public async Task<IEnumerable<Try<NotUsed>>> AsyncWriteMessages(
            IEnumerable<AtomicWrite> messages)
        {
            var serializedTries = Serializer.Serialize(messages);
            var rows = serializedTries.SelectMany(serializedTry =>
            {
                if (serializedTry.IsSuccess)
                {
                    return serializedTry.Get();
                }

                return new List<JournalRow>(0);
            }).ToList();

            try
            {
                await queueWriteJournalRows(rows);
                return serializedTries
                    .Select(r =>
                        r.IsSuccess == false
                            ? new Try<NotUsed>(r.Failure.Value)
                            : new Try<NotUsed>(NotUsed.Instance))
                    .ToImmutableList();
            }
            catch (Exception e)
            {
                return serializedTries.Select(r => new Try<NotUsed>(e));
            }
            /*.ContinueWith(task =>
               {
                   return serializedTries.Select(r =>
                       r.IsSuccess
                           ? (task.IsFaulted ? new Try<NotUsed>(task.Exception) : new Try<NotUsed>(NotUsed.Instance))
                           : new Try<NotUsed>(r.Failure.Value));
               });*/
            
            
           // var resultWhenWriteComplete = 
             //   serializedTries.All(t=>t.IsSuccess)? null : 
            /*var taskList = serializedTries.Select(async r =>
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
            
            var returnFactory =
                Task<IEnumerable<Try<NotUsed>>>.Factory.ContinueWhenAll(
                    taskList,
                    (Task<Try<NotUsed>>[] task) =>
                    {
                        return task.Select(t => t.Result);
                    });
            return returnFactory;*/
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
        public override Source<Try<Linq2DbWriteJournal.ReplayCompletion>, NotUsed> MessagesClass(DataConnection db, string persistenceId, long fromSequenceNr, long toSequenceNr,
            long max)
        {
            
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
                            return new
                                Try<Linq2DbWriteJournal.ReplayCompletion>(
                                    new Linq2DbWriteJournal.ReplayCompletion()
                                    {
                                        repr = sertry.Success.Value.Item1,
                                        SequenceNr = sertry.Success.Value.Item3
                                    }); 
                        }
                        else
                        {
                            return new Try<Linq2DbWriteJournal.ReplayCompletion>(
                                sertry.Failure.Value);
                        }
                    });
            }
        }
        public override Source<Try<(IPersistentRepresentation, long)>, NotUsed> Messages(DataConnection db, string persistenceId, long fromSequenceNr, long toSequenceNr,
            long max)
        {
            
            {
                IQueryable<JournalRow> query = db.GetTable<JournalRow>()
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
                return Source.From(query)
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

        public ISourceQueueWithComplete<WriteQueueEntry> WriteQueue;
    }
}