using System;
using System.Text;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit.Xunit2.Internals;
using JetBrains.dotMemoryUnit;
using JetBrains.dotMemoryUnit.Kernel;
using Xunit;
using Xunit.Abstractions;
using Config = Docker.DotNet.Models.Config;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{

    public class DerpSpec
    {
        private byte[][] _bytes;
        public DerpSpec(ITestOutputHelper outputHelper)
        {
            Output = outputHelper;
            DotMemoryUnitTestOutput.SetOutputMethod(o=>outputHelper.WriteLine(o));
            var rand = new Random();
            var arraySize = 1024 * 1024 * 256;
            _bytes = new byte[10][];
            for (int i = 0; i < 10; i++)
            {
                _bytes[i] = new byte[arraySize];
                //for (int j = 0; j < arraySize; j++)
                //{
                    rand.NextBytes(_bytes[i]);
                //}
            }
        }

        public ITestOutputHelper Output { get; set; }

        [DotMemoryUnit(CollectAllocations = true)]
        [Fact]
        public void ConvertBinaryToSql_Memory_Usage()
        {
            dotMemory.Check((mem) =>
                {
                    DoOldBench();
                }
            );
            dotMemory.Check((mem) =>
                {
                    DoOldBench();
                }
            );
            dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }
        [DotMemoryUnit(CollectAllocations = true)]
        [Fact]
        public void ConvertBinaryToSqlNew_Memory_Usage()
        {
            dotMemory.Check((mem) =>
                {
                    DoNewBench();
                }
            );
            dotMemory.Check((mem) =>
                {
                    DoNewBench();
                }
            );
            dotMemoryApi.SaveCollectedData(@"c:\temp\dotmemory");
        }
        private void DoOldBench()
        {
            long byteCtr = 0;
            long charCtr = 0;
            foreach (var VARIABLE in _bytes)
            {
                byteCtr = byteCtr + VARIABLE.Length;
                var sb = new StringBuilder();
                ConvertBinaryToSql(sb, VARIABLE);
                charCtr = charCtr +
                          sb.Length;
            }

            //Output.WriteLine(
            //    $"{DateTime.Now.ToString("s")} - Wrote {byteCtr} bytes into {charCtr} chars");
        }
        private void DoNewBench()
        {
            long byteCtr = 0;
            long charCtr = 0;
            foreach (var VARIABLE in _bytes)
            {
                byteCtr = byteCtr + VARIABLE.Length;
                var sb = new StringBuilder();
                ConvertBinaryToSqlNew(sb, VARIABLE);
                charCtr = charCtr +
                          sb.Length;
            }

            //Output.WriteLine(
            //    $"{DateTime.Now.ToString("s")} - Wrote {byteCtr} bytes into {charCtr} chars");
        }

        static void ConvertBinaryToSql(StringBuilder stringBuilder, byte[] value)
        {
            
            stringBuilder.Append("0x");

            foreach (var b in value)
                stringBuilder.Append(b.ToString("X2"));
        }

        static void ConvertBinaryToSqlNew(StringBuilder stringBuilder,
            byte[] value)
        {
            stringBuilder.Append(HexStr(value));
        }
        public static string HexStr(byte[] p) {
            char[] c = new char[p.Length * 2 + 2];

            byte b;

            c[0] = '0'; c[1] = 'x';

            for(int y= 0, x= 2; y<p.Length; ++y, ++x) {

                b = ((byte)(p[y] >> 4));

                c[x] = (char)(b > 9 ? b + 0x37 : b + 0x30);

                b = ((byte)(p[y] & 0xF));

                c[++x] = (char)(b > 9 ? b + 0x37 : b + 0x30);

            }
            return new string(c);
        }
    }
    public abstract class CompatibilitySpec
    {
        

        public CompatibilitySpec(ITestOutputHelper outputHelper)
        {
            Output = outputHelper;
        }

        public ITestOutputHelper Output { get; set; }

        protected void InitializeLogger(ActorSystem system)
        {
            if (Output != null)
            {
                var extSystem = (ExtendedActorSystem)system;
                var logger = extSystem.SystemActorOf(Props.Create(() => new TestOutputLogger(Output)), "log-test");
                logger.Tell(new InitializeLogger(system.EventStream));
            }
        }

        protected abstract Configuration.Config Config { get; }

        protected abstract string OldJournal { get; }
        protected abstract string NewJournal { get; }
        [Fact]
        public async Task Can_Recover_SqlCommon_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                Config);
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor(OldJournal,
                    "p-1")), "test");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new PersistActor(NewJournal,
                    "p-1")), "test");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(5)).Result);
        }
        [Fact]
        public async Task Can_Persist_SqlCommon_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                Config);
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor(OldJournal,
                    "p-2")), "test");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new PersistActor(NewJournal,
                    "p-2")), "test");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(10)).Result);
            var ourSecondGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourSecondGuid, Number = 2});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourSecondGuid},TimeSpan.FromSeconds(5)).Result);
        }
        
        [Fact]
        public async Task SqlCommon_Journal_Can_Recover_L2Db_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                Config);
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor(NewJournal,
                    "p-3")), "test");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor(OldJournal,
                    "p-3")), "test");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(5)).Result);
        }
        [Fact]
        public async Task SqlCommon_Journal_Can_Persist_L2db_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                Config);
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor(NewJournal,
                    "p-4")), "test");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new PersistActor(OldJournal,
                    "p-4")), "test");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(10)).Result);
            var ourSecondGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourSecondGuid, Number = 2});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourSecondGuid},TimeSpan.FromSeconds(5)).Result);
        }
    }
}