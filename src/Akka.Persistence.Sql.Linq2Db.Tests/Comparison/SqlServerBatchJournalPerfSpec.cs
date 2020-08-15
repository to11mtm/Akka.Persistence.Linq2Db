using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Tests.Performance;
using JetBrains.dotMemoryUnit;
using LinqToDB;
using LinqToDB.Data;
using Xunit.Abstractions;
using Config = Akka.Configuration.Config;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class SqlServerBatchJournalPerfSpec : L2dbJournalPerfSpec
    {
        public SqlServerBatchJournalPerfSpec(ITestOutputHelper output) : base(InitConfig(),"sqlserverperfspec", output)
        {
            DotMemoryUnitTestOutput.SetOutputMethod(
                message => output.WriteLine(message));
            using (var conn =
                new DataConnection(ProviderName.SqlServer2008, connString.Replace("\\\\","\\")))
            {
                conn.GetTable<JournalRow>().TableName("EventJournal_batch").Delete();
                //Akka.Persistence.SqlServer.Journal.BatchingSqlServerJournal
            }
        }
        private static string connString =
            "Data Source=(LocalDB)\\\\mssqllocaldb";
        public static Config InitConfig()
        {
            DbUtils.ConnectionString = connString;
            //need to make sure db is created before the tests start
            //DbUtils.Initialize(connString);
            var specString = $@"
﻿akka.actor {{
	serializers {{
		hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
    }}
    serialization-bindings {{
      ""System.Object"" = hyperion
    }}
}}
                    akka.persistence {{
                        publish-plugin-commands = on
                        journal {{
                            plugin = ""akka.persistence.journal.sql-server""
                            sql-server {{
                                class = ""Akka.Persistence.SqlServer.Journal.BatchingSqlServerJournal, Akka.Persistence.SqlServer""
                                plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                                table-name = EventJournal_batch
                                schema-name = dbo
                                auto-initialize = on
                                connection-string = ""{DbUtils.ConnectionString}""
                            }}
                        }}
dispatchers {{
        # Dispatcher used by every plugin which does not declare explicit
        # `plugin-dispatcher` field.
        default-plugin-dispatcher {{
            type = PinnedDispatcher
            executor = ""fork-join-executor""
        }}


        # Default dispatcher for message replay.
        default-replay-dispatcher {{
            type = ForkJoinDispatcher
			executor = ""fork-join-executor""
            dedicated-thread-pool {{
                # Fixed number of threads to have in this threadpool
                thread-count = 8
            }}
        }}
        # Default dispatcher for streaming snapshot IO
        default-stream-dispatcher {{
            type = ForkJoinDispatcher
            dedicated-thread-pool {{
                # Fixed number of threads to have in this threadpool
                thread-count = 8
            }}
        }}
    }}
                    }}";

            return ConfigurationFactory.ParseString(specString);
        }
    }
}