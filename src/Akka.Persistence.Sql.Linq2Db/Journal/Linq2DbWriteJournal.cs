using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Journal;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Util;
using Akka.Util.Internal;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class Linq2DbWriteJournal : AsyncWriteJournal
    {
        private ActorMaterializer _mat;
        private JournalConfig _journalConfig;
        private ByteArrayJournalDao _journal;

        public Linq2DbWriteJournal(Config config)
        {
            _mat = ActorMaterializer.Create(Context);
            _journalConfig = new JournalConfig(config);
            try
            {
                _journal = new ByteArrayJournalDao( Context.System.Scheduler.Advanced ,_mat,
                    new AkkaPersistenceDataConnectionFactory(_journalConfig),
                    _journalConfig, Context.System.Serialization);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            
            if (_journalConfig.TableConfiguration.AutoInitialize)
            {
                try
                {
                    _journal.InitializeTables(); 
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                
            }
        }

        protected override bool ReceivePluginInternal(object message)
        {
            if (message is WriteFinished wf)
            {
                writeInProgress.Remove(wf.PersistenceId);
            }
            else
            {
                return false;
            }

            return true;
        }

        public override void AroundPreRestart(Exception cause, object message)
        {
            Context.System.Log.Error(cause,"WAT");
            base.AroundPreRestart(cause, message);
        }

        public override Task ReplayMessagesAsync(IActorContext context, string persistenceId,
            long fromSequenceNr, long toSequenceNr, long max, Action<IPersistentRepresentation> recoveryCallback)
        {

            return _journal.MessagesWithBatch(persistenceId, fromSequenceNr,
                    toSequenceNr, _journalConfig.DaoConfig.BatchSize,
                    Option<(TimeSpan, SchedulerBase)>.None)
                .Take(max).SelectAsync(1,
                    t => t.IsSuccess
                        ? Task.FromResult(t.Success.Value)
                        : Task.FromException<(IPersistentRepresentation, long)>(
                            t.Failure.Value))
                .RunForeach(r =>
                {
                    recoveryCallback(r.Item1);
                }, _mat);
        }

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            if (writeInProgress.ContainsKey(persistenceId))
            {
                await writeInProgress[persistenceId];
                var hsn =await _journal.HighestSequenceNr(persistenceId,
                    fromSequenceNr);
                return hsn;
                /*return await writeInProgress[persistenceId].ContinueWith(task =>
                {
                    return  _journal.HighestSequenceNr(persistenceId, fromSequenceNr);
                        /*.ContinueWith(hsn =>
                        {
                            if (hsn.IsFaulted)
                            {
                                tcs.SetException(hsn.Exception);
                            }
                            else
                            {
                                tcs.SetResult(hsn.Result);    
                            }
                        });
                });*/
            }
            return await _journal.HighestSequenceNr(persistenceId, fromSequenceNr);
        }
        private Dictionary<string,Task> writeInProgress = new Dictionary<string, Task>();
        protected override async Task<IImmutableList<Exception>> WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            //TODO: CurrentTimeMillis;
            var future =  _journal.AsyncWriteMessages(messages);
            var persistenceId = messages.Head().PersistenceId;
            writeInProgress.AddOrSet(persistenceId,future);
            var ourTcs = new TaskCompletionSource<ImmutableList<Exception>>();
            //Task<IImmutableList<Exception>>.Factory.
            future.ContinueWith(( p)=>
                Context.Self.Tell(new WriteFinished(persistenceId, future)));
            
            return await future.ContinueWith(task =>
            {
                var finalResult =
                    task.Result
                        .Select(r => r.IsSuccess ? null : TryUnwrapException(r.Failure.Value))
                        .ToImmutableList() as IImmutableList<Exception>;
                return finalResult;
            });
        }

        protected override Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            return _journal.Delete(persistenceId, toSequenceNr);
        }
    }
}