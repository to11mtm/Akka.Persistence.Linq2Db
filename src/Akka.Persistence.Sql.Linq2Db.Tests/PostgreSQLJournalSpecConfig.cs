using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public static class PostgreSQLJournalSpecConfig
    {
        public static string _journalBaseConfig = @"
            akka.persistence {{
                publish-plugin-commands = on
                journal {{
                    plugin = ""akka.persistence.journal.testspec""
                    testspec {{
                        class = ""{0}""
                        #plugin-dispatcher = ""akka.actor.default-dispatcher""
plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                        connection-string = ""{1}""
#connection-string = ""FullUri=file:test.db&cache=shared""
                        provider-name = ""{2}""
                        tables.journal {{ auto-init = true }}
                    }}
                }}
            }}
        ";
        
        public static Config Create(string connString, string providerName)
        {
            return ConfigurationFactory.ParseString(
                string.Format(_journalBaseConfig,
                    typeof(Linq2DbWriteJournal).AssemblyQualifiedName,
                    connString, providerName));
        }
    }
}