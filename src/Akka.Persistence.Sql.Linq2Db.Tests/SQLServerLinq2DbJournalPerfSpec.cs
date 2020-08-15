using System;
using System.Threading;
using System.Threading.Tasks;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Tests.Performance;
using Akka.Persistence.TestKit.Performance;
using JetBrains.dotMemoryUnit;
using LinqToDB;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    /*public class SQLServerOldJournalPerfSpec : JournalPerfSpec
    {
        private static string connString =
            "Data Source=(LocalDB)\\\\mssqllocaldb";
        private static readonly  Config conf = SQLServerJournalSpecConfig.Create(connString,"journalPerfSpec");
        public SQLServerOldJournalPerfSpec(ITestOutputHelper output)
            : base(conf, "SQLServer", output)
        {
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
        
    }*/
    public class SQLServerLinq2DbJournalPerfSpec : L2dbJournalPerfSpec
    {
        private static string connString =
            "Data Source=(LocalDB)\\\\mssqllocaldb";
        private static readonly  Config conf = SQLServerJournalSpecConfig.Create(connString,"journalPerfSpec");
        public SQLServerLinq2DbJournalPerfSpec(ITestOutputHelper output)
            : base(conf, "SQLServer", output)
        {
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