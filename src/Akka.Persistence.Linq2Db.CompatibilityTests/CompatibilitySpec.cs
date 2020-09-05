using System;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Event;
using Akka.TestKit.Xunit2.Internals;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class CompatibilitySpec
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
        [Fact]
        public async Task Can_Recover_SqlServer_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                SqlServerCompatibilitySpecConfig.InitConfig("journal_compat",
                    "journal_metadata_compat"));
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor("akka.persistence.journal.sql-server",
                    "p-1")), "test");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new PersistActor("akka.persistence.journal.testspec",
                    "p-1")), "test");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(5)).Result);
        }
        [Fact]
        public async Task Can_Persist_SqlServer_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                SqlServerCompatibilitySpecConfig.InitConfig("journal_compat",
                    "journal_metadata_compat"));
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor("akka.persistence.journal.sql-server",
                    "p-2")), "test");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new PersistActor("akka.persistence.journal.testspec",
                    "p-2")), "test");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(10)).Result);
            var ourSecondGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourSecondGuid, Number = 2});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourSecondGuid},TimeSpan.FromSeconds(5)).Result);
        }
        
        [Fact]
        public async Task SQLServer_Journal_Can_Recover_L2Db_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                SqlServerCompatibilitySpecConfig.InitConfig("journal_compat",
                    "journal_metadata_compat"));
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor("akka.persistence.journal.testspec",
                    "p-3")), "test");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor("akka.persistence.journal.sql-server",
                    "p-3")), "test");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(5)).Result);
        }
        [Fact]
        public async Task SQLServer_Journal_Can_Persist_L2db_Journal()
        {
            var sys1 = ActorSystem.Create("first",
                SqlServerCompatibilitySpecConfig.InitConfig("journal_compat",
                    "journal_metadata_compat"));
            InitializeLogger(sys1);
            var persistRef = sys1.ActorOf(Props.Create(() =>
                new PersistActor("akka.persistence.journal.testspec",
                    "p-4")), "test");
            var ourGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourGuid, Number = 1});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid}, TimeSpan.FromSeconds(5)).Result);
            await persistRef.GracefulStop(TimeSpan.FromSeconds(5));
            persistRef =  sys1.ActorOf(Props.Create(() =>
                new PersistActor("akka.persistence.journal.sql-server",
                    "p-4")), "test");
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourGuid},TimeSpan.FromSeconds(10)).Result);
            var ourSecondGuid = Guid.NewGuid();
            persistRef.Tell(new SomeEvent(){EventName = "rec-test", Guid = ourSecondGuid, Number = 2});
            Assert.True(persistRef.Ask<bool>(new ContainsEvent(){Guid = ourSecondGuid},TimeSpan.FromSeconds(5)).Result);
        }
    }
}