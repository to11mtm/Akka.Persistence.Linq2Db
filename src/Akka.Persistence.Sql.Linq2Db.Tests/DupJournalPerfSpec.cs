//-----------------------------------------------------------------------
// <copyright file="JournalPerfSpec.cs" company="Akka.NET Project">
//     Copyright (C) 2009-2020 Lightbend Inc. <http://www.lightbend.com>
//     Copyright (C) 2013-2020 .NET Foundation <https://github.com/akkadotnet/akka.net>
// </copyright>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.TestKit;
using Akka.Util.Internal;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests.Performance
{
    public abstract class L2dbJournalPerfSpec : Akka.TestKit.Xunit2.TestKit
    {
        private TestProbe testProbe;

        // Number of messages sent to the PersistentActor under test for each test iteration
        private int EventsCount;

        // Number of measurement iterations each test will be run.
        private const int MeasurementIterations = 10;

        private IReadOnlyList<int> Commands => Enumerable.Range(1, EventsCount).ToList();

        private TimeSpan ExpectDuration;

        protected L2dbJournalPerfSpec(Config config, string actorSystem, ITestOutputHelper output,int timeoutDurationSeconds = 30, int eventsCount = 10000)
            : base(config ?? Config.Empty, actorSystem, output)
        {
            EventsCount = eventsCount;
            ExpectDuration = TimeSpan.FromSeconds(timeoutDurationSeconds);
            testProbe = CreateTestProbe();
        }
        
        internal IActorRef BenchActor(string pid, int replyAfter)
        {
            return Sys.ActorOf(Props.Create(() => new BenchActor(pid, testProbe, EventsCount)));;
        }
        internal (IActorRef aut,TestProbe probe) BenchActorNewProbe(string pid, int replyAfter)
        {
            var tp = CreateTestProbe();
            return (Sys.ActorOf(Props.Create(() => new BenchActor(pid, tp, EventsCount))), tp);
        }

        internal void FeedAndExpectLast(IActorRef actor, string mode, IReadOnlyList<int> commands)
        {
            commands.ForEach(c => actor.Tell(new Cmd(mode, c)));
            testProbe.ExpectMsg(commands.Last(), ExpectDuration);
        }
        internal void FeedAndExpectLastSpecific((IActorRef actor, TestProbe probe) aut, string mode, IReadOnlyList<int> commands)
        {
            commands.ForEach(c => aut.actor.Tell(new Cmd(mode, c)));
            
            aut.probe.ExpectMsg(commands.Last(), ExpectDuration);
        }
        internal void Measure(Func<TimeSpan, string> msg, Action block)
        {
            var measurements = new List<TimeSpan>(MeasurementIterations);

            block(); //warm-up

            int i = 0;
            while (i < MeasurementIterations)
            {
                var sw = Stopwatch.StartNew();
                block();
                sw.Stop();
                measurements.Add(sw.Elapsed);
                Output.WriteLine(msg(sw.Elapsed));
                i++;
            }

            double avgTime = measurements.Select(c => c.TotalMilliseconds).Sum() / MeasurementIterations;
            double msgPerSec = (EventsCount / avgTime) * 1000;

            Output.WriteLine($"Average time: {avgTime} ms, {msgPerSec} msg/sec");
        }
        
        [DotMemoryUnit(CollectAllocations=true, FailIfRunWithoutSupport = false)]
        [Fact]
        public void DotMemory_PersistenceActor_performance_must_measure_Persist()
        {
            dotMemory.Check();
            
            var p1 = BenchActor("DotMemoryPersistPid", EventsCount);
            
            dotMemory.Check((mem) =>
                {
                    Measure(
                        d =>
                            $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                        () =>
                        {
                            FeedAndExpectLast(p1, "p", Commands);
                            p1.Tell(ResetCounter.Instance);
                        });
                }
            );
            dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }
        
        
        [Fact]
        public void PersistenceActor_performance_must_measure_Persist()
        {
            
            var p1 = BenchActor("PersistPid", EventsCount);
            
            //dotMemory.Check((mem) =>
            //{
                Measure(
                    d =>
                        $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        FeedAndExpectLast(p1, "p", Commands);
                        p1.Tell(ResetCounter.Instance);
                    });
            //}
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }
        
        //[DotMemoryUnit(CollectAllocations=true, FailIfRunWithoutSupport = false)]
        [Fact]
        public void PersistenceActor_performance_must_measure_PersistDouble()
        {
            //  dotMemory.Check();
            
            var p1 = BenchActorNewProbe("DoublePersistPid1", EventsCount);
            var p2 = BenchActorNewProbe("DoublePersistPid2", EventsCount);
            //dotMemory.Check((mem) =>
            {
                Measure(
                    d =>
                        $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        var t1 = Task.Run(() => FeedAndExpectLastSpecific(p1, "p", Commands));
                        var t2 = Task.Run(()=>FeedAndExpectLastSpecific(p2, "p", Commands));
                        Task.WhenAll(new[] {t1, t2}).Wait();
                        p1.aut.Tell(ResetCounter.Instance);
                        p2.aut.Tell(ResetCounter.Instance);
                    });
            }
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }
        [Fact]
        public void PersistenceActor_performance_must_measure_PersistTriple()
        {
            //  dotMemory.Check();
            
            var p1 = BenchActorNewProbe("TriplePersistPid1", EventsCount);
            var p2 = BenchActorNewProbe("TriplePersistPid2", EventsCount);
            var p3 = BenchActorNewProbe("TriplePersistPid3", EventsCount);
            //dotMemory.Check((mem) =>
            {
                Measure(
                    d =>
                        $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        var t1 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p1, "p", Commands));
                        var t2 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p2, "p", Commands));
                        var t3 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p3, "p", Commands));
                        Task.WhenAll(new[] {t1, t2, t3}).Wait();
                        p1.aut.Tell(ResetCounter.Instance);
                        p2.aut.Tell(ResetCounter.Instance);
                        p3.aut.Tell(ResetCounter.Instance);
                    });
            }
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }
        
        [Fact]
        public void PersistenceActor_performance_must_measure_PersistQuad()
        {
            //  dotMemory.Check();
            
            var p1 = BenchActorNewProbe("QuadPersistPid1", EventsCount);
            var p2 = BenchActorNewProbe("QuadPersistPid2", EventsCount);
            var p3 = BenchActorNewProbe("QuadPersistPid3", EventsCount);
            var p4 = BenchActorNewProbe("QuadPersistPid4", EventsCount);
            //dotMemory.Check((mem) =>
            {
                Measure(
                    d =>
                        $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        var t1 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p1, "p", Commands));
                        var t2 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p2, "p", Commands));
                        var t3 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p3, "p", Commands));
                        var t4 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p4, "p", Commands));
                        Task.WhenAll(new[] {t1, t2, t3,t4}).Wait();
                        p1.aut.Tell(ResetCounter.Instance);
                        p2.aut.Tell(ResetCounter.Instance);
                        p3.aut.Tell(ResetCounter.Instance);
                        p4.aut.Tell(ResetCounter.Instance);
                    });
            }
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }
        
        [Fact]
        public void PersistenceActor_performance_must_measure_PersistOct()
        {
            //  dotMemory.Check();
            
            var p1 = BenchActorNewProbe("OctPersistPid1", EventsCount);
            var p2 = BenchActorNewProbe("OctPersistPid2", EventsCount);
            var p3 = BenchActorNewProbe("OctPersistPid3", EventsCount);
            var p4 = BenchActorNewProbe("OctPersistPid4", EventsCount);
            var p5 = BenchActorNewProbe("OctPersistPid5", EventsCount);
            var p6 = BenchActorNewProbe("OctPersistPid6", EventsCount);
            var p7 = BenchActorNewProbe("OctPersistPid7", EventsCount);
            var p8 = BenchActorNewProbe("OctPersistPid8", EventsCount);
            //dotMemory.Check((mem) =>
            {
                Measure(
                    d =>
                        $"Persist()-ing {EventsCount} took {d.TotalMilliseconds} ms",
                    () =>
                    {
                        var t1 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p1, "p", Commands));
                        var t2 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p2, "p", Commands));
                        var t3 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p3, "p", Commands));
                        var t4 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p4, "p", Commands));
                        var t5 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p5, "p", Commands));
                        var t6 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p6, "p", Commands));
                        var t7 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p7, "p", Commands));
                        var t8 = Task.Run(() =>
                            FeedAndExpectLastSpecific(p8, "p", Commands));
                        Task.WhenAll(new[] {t1, t2, t3,t4,t5,t6,t7,t8}).Wait();
                        p1.aut.Tell(ResetCounter.Instance);
                        p2.aut.Tell(ResetCounter.Instance);
                        p3.aut.Tell(ResetCounter.Instance);
                        p4.aut.Tell(ResetCounter.Instance);
                        p5.aut.Tell(ResetCounter.Instance);
                        p6.aut.Tell(ResetCounter.Instance);
                        p7.aut.Tell(ResetCounter.Instance);
                        p8.aut.Tell(ResetCounter.Instance);
                    });
            }
            //);
            //dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistAll()
        {
            var p1 = BenchActor("PersistAllPid", EventsCount);
            Measure(d => $"PersistAll()-ing {EventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                FeedAndExpectLast(p1, "pb", Commands);
                p1.Tell(ResetCounter.Instance);
            });
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistAsync()
        {
            var p1 = BenchActor("PersistAsyncPid", EventsCount);
            Measure(d => $"PersistAsync()-ing {EventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                FeedAndExpectLast(p1, "pa", Commands);
                p1.Tell(ResetCounter.Instance);
            });
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_PersistAllAsync()
        {
            var p1 = BenchActor("PersistAllAsyncPid", EventsCount);
            Measure(d => $"PersistAllAsync()-ing {EventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                FeedAndExpectLast(p1, "pba", Commands);
                p1.Tell(ResetCounter.Instance);
            });
        }

        [Fact]
        public void PersistenceActor_performance_must_measure_Recovering()
        {
            var p1 = BenchActor("PersistRecoverPid", EventsCount);

            FeedAndExpectLast(p1, "p", Commands);
            Measure(d => $"Recovering {EventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                BenchActor("PersistRecoverPid", EventsCount);
                testProbe.ExpectMsg(Commands.Last(), ExpectDuration);
            });
        }
        
        [Fact]
        public void PersistenceActor_performance_must_measure_RecoveringTwo()
        {
            var p1 = BenchActorNewProbe("DoublePersistRecoverPid1", EventsCount);
            var p2 = BenchActorNewProbe("DoublePersistRecoverPid2", EventsCount);
            FeedAndExpectLastSpecific(p1, "p", Commands);
            FeedAndExpectLastSpecific(p2, "p", Commands);
            Measure(d => $"Recovering {EventsCount} took {d.TotalMilliseconds} ms", () =>
            {
               var task1 = Task.Run(()=>
               {
                   var refAndProbe =BenchActorNewProbe("DoublePersistRecoverPid1",
                           EventsCount);
                   refAndProbe.probe.ExpectMsg(Commands.Last(), ExpectDuration);
               });
               var task2 =Task.Run(() =>
               {
                   var refAndProbe =BenchActorNewProbe("DoublePersistRecoverPid2", EventsCount);
                   refAndProbe.probe.ExpectMsg(Commands.Last(), ExpectDuration);
               });
               Task.WaitAll(new[] {task1, task2});

            });
        }
        [Fact]
        public void PersistenceActor_performance_must_measure_RecoveringFour()
        {
            var p1 = BenchActorNewProbe("QuadPersistRecoverPid1", EventsCount);
            var p2 = BenchActorNewProbe("QuadPersistRecoverPid2", EventsCount);
            var p3 = BenchActorNewProbe("QuadPersistRecoverPid3", EventsCount);
            var p4 = BenchActorNewProbe("QuadPersistRecoverPid4", EventsCount);
            FeedAndExpectLastSpecific(p1, "p", Commands);
            FeedAndExpectLastSpecific(p2, "p", Commands);
            FeedAndExpectLastSpecific(p3, "p", Commands);
            FeedAndExpectLastSpecific(p4, "p", Commands);
            Measure(d => $"Recovering {EventsCount} took {d.TotalMilliseconds} ms", () =>
            {
                var task1 = Task.Run(()=>
                {
                    var refAndProbe =BenchActorNewProbe("QuadPersistRecoverPid1",
                        EventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), ExpectDuration);
                });
                var task2 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("QuadPersistRecoverPid2", EventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), ExpectDuration);
                });
                var task3 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("QuadPersistRecoverPid3", EventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), ExpectDuration);
                });
                var task4 =Task.Run(() =>
                {
                    var refAndProbe =BenchActorNewProbe("QuadPersistRecoverPid4", EventsCount);
                    refAndProbe.probe.ExpectMsg(Commands.Last(), ExpectDuration);
                });
                Task.WaitAll(new[] {task1, task2,task3,task4});

            });
        }
    }

    internal class ResetCounter
    {
        public static ResetCounter Instance { get; } = new ResetCounter();
        private ResetCounter() { }
    }

    public class Cmd
    {
        public Cmd(string mode, int payload)
        {
            Mode = mode;
            Payload = payload;
        }

        public string Mode { get; }

        public int Payload { get; }
    }

    internal class BenchActor : UntypedPersistentActor
    {
        private int _counter = 0;
        private const int BatchSize = 50;
        private List<Cmd> _batch = new List<Cmd>(BatchSize);

        public BenchActor(string persistenceId, IActorRef replyTo, int replyAfter)
        {
            PersistenceId = persistenceId;
            ReplyTo = replyTo;
            ReplyAfter = replyAfter;
        }

        public override string PersistenceId { get; }
        public IActorRef ReplyTo { get; }
        public int ReplyAfter { get; }

        protected override void OnRecover(object message)
        {
            switch (message)
            {
                case Cmd c:
                    _counter++;
                    if (c.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{c.Payload}]");
                    if (_counter == ReplyAfter) ReplyTo.Tell(c.Payload);
                    break;
            }
        }

        protected override void OnCommand(object message)
        {
            switch (message)
            {
                case Cmd c when c.Mode == "p":
                    Persist(c, d =>
                    {
                        _counter += 1;
                        if (d.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                        if (_counter == ReplyAfter) ReplyTo.Tell(d.Payload);
                    });
                    break;
                case Cmd c when c.Mode == "pb":
                    _batch.Add(c);

                    if (_batch.Count % BatchSize == 0)
                    {
                        PersistAll(_batch, d =>
                        {
                            _counter += 1;
                            if (d.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                            if (_counter == ReplyAfter) ReplyTo.Tell(d.Payload);
                        });
                        _batch = new List<Cmd>(BatchSize);
                    }
                    break;
                case Cmd c when c.Mode == "pa":
                    PersistAsync(c, d =>
                    {
                        _counter += 1;
                        if (d.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                        if (_counter == ReplyAfter) ReplyTo.Tell(d.Payload);
                    });
                    break;
                case Cmd c when c.Mode == "pba":
                    _batch.Add(c);

                    if (_batch.Count % BatchSize == 0)
                    {
                        PersistAllAsync(_batch, d =>
                        {
                            _counter += 1;
                            if (d.Payload != _counter) throw new ArgumentException($"Expected to receive [{_counter}] yet got: [{d.Payload}]");
                            if (_counter == ReplyAfter) ReplyTo.Tell(d.Payload);
                        });
                        _batch = new List<Cmd>(BatchSize);
                    }
                    break;
                case ResetCounter _:
                    _counter = 0;
                    break;
            }
        }
    }
}
