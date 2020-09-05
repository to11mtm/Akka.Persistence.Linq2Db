using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db;
using Akka.Persistence.Sql.Linq2Db.Journal.Journal;
using Akka.Persistence.Sql.Linq2Db.Journal.Journal.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Journal.Types;
using Akka.Persistence.Sql.Linq2Db.Tests;
using JetBrains.dotMemoryUnit;
using LinqToDB;
using Xunit.Abstractions;

namespace Akka.Persistence.Linq2Db.BenchmarkTests.Local.Linq2Db
{
    public class SQLServerLinq2DbJournalPerfSpec : L2dbJournalPerfSpec
    {
        
        private static readonly  Config conf = SQLServerJournalSpecConfig.Create(ConnectionString.Instance,"journalPerfSpec");
        public SQLServerLinq2DbJournalPerfSpec(ITestOutputHelper output)
            : base(conf, "SQLServer", output, eventsCount: TestConstants.NumMessages)
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