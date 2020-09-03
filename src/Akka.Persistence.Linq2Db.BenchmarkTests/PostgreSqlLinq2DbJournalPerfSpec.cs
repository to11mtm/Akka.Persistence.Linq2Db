using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Tests.Performance;
using JetBrains.dotMemoryUnit;
using LinqToDB;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class PostgreSqlLinq2DbJournalPerfSpec : L2dbJournalPerfSpec
    {
        
        private static readonly  Config conf = PostgreSQLJournalSpecConfig.Create(PostgreSQLJournalSpec.connString,ProviderName.PostgreSQL95);
        public PostgreSqlLinq2DbJournalPerfSpec(ITestOutputHelper output)
            : base(conf, "PostgreSql", output, eventsCount: 10000)
        {
            //LinqToDB.Common.Configuration.ContinueOnCapturedContext = false;
            DotMemoryUnitTestOutput.SetOutputMethod(
                message => output.WriteLine(message));
            var connFactory = new AkkaPersistenceDataConnectionFactory(new JournalConfig(conf.GetConfig("akka.persistence.journal.testspec")));
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
        
    }
}