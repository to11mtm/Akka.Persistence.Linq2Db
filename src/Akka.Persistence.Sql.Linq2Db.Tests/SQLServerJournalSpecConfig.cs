using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public static class SQLServerJournalSpecConfig
    {
        /*﻿akka.actor {{
	serializers {{
		hyperion = ""Akka.Serialization.HyperionSerializer, Akka.Serialization.Hyperion""
    }}
    serialization-bindings {{
      ""System.Object"" = hyperion
    }}
}}*/
        public static string _journalBaseConfig = @"

akka.persistence {{
dispatchers {{
        # Dispatcher used by every plugin which does not declare explicit
        # `plugin-dispatcher` field.
        default-plugin-dispatcher {{
            type = PinnedDispatcher
            executor = ""fork-join-executor""
        }}

        # Default dispatcher for message replay.
        test-forkjoin-dispatcher {{
            type = ForkJoinDispatcher
			executor = ""fork-join-executor""
            dedicated-thread-pool {{
                # Fixed number of threads to have in this threadpool
                thread-count = 8
            }}
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
                publish-plugin-commands = on
                journal {{
                    plugin = ""akka.persistence.journal.testspec""
                    testspec {{
                        class = ""{0}""
                        plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
#plugin-dispatcher = ""akka.actor.default-dispatcher""
                        connection-string = ""{1}""
#connection-string = ""FullUri=file:test.db&cache=shared""
                        provider-name = """ + LinqToDB.ProviderName.SqlServer2017 + @"""
                        tables.journal {{ 
                           auto-init = true
                           table-name = ""{2}"" 
                           }}
                    }}
                }}
            }}
        ";
        public static Config Create(string connString, string tableName)
        {
            return ConfigurationFactory.ParseString(
                string.Format(_journalBaseConfig,
                    typeof(Linq2DbWriteJournal).AssemblyQualifiedName,
                    connString,tableName));
        }
    }
}