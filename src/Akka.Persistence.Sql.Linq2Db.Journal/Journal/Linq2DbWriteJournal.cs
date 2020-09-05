using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
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
            try
            {
                _mat = ActorMaterializer.Create(Context,
                    ActorMaterializerSettings.Create(Context.System)
                        .WithDispatcher(
                            "akka.stream.default-blocking-io-dispatcher"),
                    "l2dbWriteJournal"
                );
                _journalConfig = new JournalConfig(config);
                try
                {
                    _journal = new ByteArrayJournalDao(
                        Context.System.Scheduler.Advanced, _mat,
                        new AkkaPersistenceDataConnectionFactory(
                            _journalConfig),
                        _journalConfig, Context.System.Serialization);
                }
                catch (Exception e)
                {
                    Context.GetLogger().Error(e, "Error Initializing Journal!");
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
                        Context.GetLogger().Warning(e,
                            "Unable to Initialize Persistence Journal Table!");
                    }

                }
            }
            catch (Exception ex)
            {
                Context.GetLogger().Warning(ex,"Unexpected error initializing journal!");
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

        public class ReplayCompletion
        {
            public IPersistentRepresentation repr { get; set; }
            public long SequenceNr { get; set; }
        }
        public override async Task ReplayMessagesAsync(IActorContext context, string persistenceId,
            long fromSequenceNr, long toSequenceNr, long max, Action<IPersistentRepresentation> recoveryCallback)
        {
            await _journal.MessagesWithBatchClass2(persistenceId, fromSequenceNr,
                    toSequenceNr, _journalConfig.DaoConfig.ReplayBatchSize,
                    Option<(TimeSpan, SchedulerBase)>.None)
                .Take(max).SelectAsync(1,
                    t => t.IsSuccess
                        ? Task.FromResult(t.Success.Value)
                        : Task.FromException<ReplayCompletion>(
                            t.Failure.Value))
                .RunForeach(r =>
                {
                    recoveryCallback(r.repr);
                }, _mat);

            /*await _journal.MessagesWithBatch(persistenceId, fromSequenceNr,
                    toSequenceNr, _journalConfig.DaoConfig.ReplayBatchSize,
                    Option<(TimeSpan, SchedulerBase)>.None)
                .Take(max).SelectAsync(1,
                    t => t.IsSuccess
                        ? Task.FromResult(t.Success.Value)
                        : Task.FromException<(IPersistentRepresentation,long)>(
                            t.Failure.Value))
                .RunForeach(r =>
                {
                    recoveryCallback(r.Item1);
                }, _mat);*/
        }

        public override async Task<long> ReadHighestSequenceNrAsync(string persistenceId, long fromSequenceNr)
        {
            if (writeInProgress.ContainsKey(persistenceId))
            {
                await writeInProgress[persistenceId];
                var hsn =await _journal.HighestSequenceNr(persistenceId,
                    fromSequenceNr);
                return hsn;
            }
            return await _journal.HighestSequenceNr(persistenceId, fromSequenceNr);
        }
        private Dictionary<string,Task> writeInProgress = new Dictionary<string, Task>();

        protected override async Task<IImmutableList<Exception>>
            WriteMessagesAsync(IEnumerable<AtomicWrite> messages)
        {
            //TODO: CurrentTimeMillis;
            var future = _journal.AsyncWriteMessages(messages);
            var persistenceId = messages.Head().PersistenceId;
            writeInProgress.AddOrSet(persistenceId, future);
            var self = Self;

            future.ContinueWith((p) =>
                    self.Tell(new WriteFinished(persistenceId, future)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            return await future;

        }

        protected override Task DeleteMessagesToAsync(string persistenceId, long toSequenceNr)
        {
            return _journal.Delete(persistenceId, toSequenceNr);
        }
    }
}