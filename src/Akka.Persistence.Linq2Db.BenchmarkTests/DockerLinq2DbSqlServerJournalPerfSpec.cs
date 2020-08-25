using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Tests.Performance;
using LinqToDB;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    [Collection("SqlServerSpec")]
    public class DockerLinq2DbSqlServerJournalPerfSpec : L2dbJournalPerfSpec
    {
        public static string _journalBaseConfig = @"
            akka.persistence {{
                publish-plugin-commands = on
                journal {{
                    plugin = ""akka.persistence.journal.testspec""
                    testspec {{
                        class = ""{0}""
                        plugin-dispatcher = ""akka.actor.default-dispatcher""
                        connection-string = ""{1}""
#connection-string = ""FullUri=file:test.db&cache=shared""
                        provider-name = """ + LinqToDB.ProviderName.SqlServer2012 + @"""
                        tables.journal {{ 
                           auto-init = true
                           table-name = ""{2}"" 
                           }}
                    }}
                }}
            }}
        ";
        
        public static Config Create(string connString)
        {
            return ConfigurationFactory.ParseString(
                string.Format(_journalBaseConfig,
                    typeof(Linq2DbWriteJournal).AssemblyQualifiedName,
                    connString,"testPerfTable"));
        }
        public DockerLinq2DbSqlServerJournalPerfSpec(ITestOutputHelper output,
            SqlServerFixture fixture) : base(InitConfig(fixture),
            "sqlserverperf", output,40, 100)
        {
            
            var connFactory = new AkkaPersistenceDataConnectionFactory(new JournalConfig(Create(DockerDbUtils.ConnectionString).GetConfig("akka.persistence.journal.testspec")));
            using (var conn = connFactory.GetConnection())
            {
                try
                {
                    conn.GetTable<JournalRow>().Delete();
                }
                catch (Exception e)
                {
                }
                
            }
        }
            
        public static Config InitConfig(SqlServerFixture fixture)
        {
            //need to make sure db is created before the tests start
            DbUtils.Initialize(fixture.ConnectionString);
            

            return Create(DbUtils.ConnectionString);
        }  
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            DbUtils.Clean();
        }
    }
}