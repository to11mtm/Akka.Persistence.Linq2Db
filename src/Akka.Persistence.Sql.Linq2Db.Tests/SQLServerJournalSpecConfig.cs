using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public static class SQLServerJournalSpecConfig
    {
        public static string _journalBaseConfig = @"
akka.persistence {{
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
                        parallelism = 3
                        use-clone-connection = true
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