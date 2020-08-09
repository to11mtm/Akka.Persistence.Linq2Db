using System;
using Akka.Configuration;
using Akka.Persistence.TestKit.Performance;
using LinqToDB;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class SQLServerLinq2DbJournalPerfSpec : JournalPerfSpec
    {
        private static string connString =
            "Data Source=(LocalDB)\\\\mssqllocaldb";
        private static readonly  Config conf = SQLServerJournalSpecConfig.Create(connString,"journalPerfSpec");
        public SQLServerLinq2DbJournalPerfSpec(ITestOutputHelper output)
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
        
    }
}