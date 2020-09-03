using System;
using Akka.Configuration;
using Akka.Util.Internal;
using LinqToDB;
using Npgsql;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class
        PostgreSQLJournalSpec : Akka.Persistence.TCK.Journal.JournalSpec
    {
        private static AtomicCounter counter = new AtomicCounter(0);

        public static string connString = new NpgsqlConnectionStringBuilder()
        {
            Host = "localhost", Username = "postgres", Password = "",
            Database = "l2dbpersist"
        }.ToString();

        private static readonly Config conf =
            PostgreSQLJournalSpecConfig.Create(connString,
                ProviderName.PostgreSQL95);

        public PostgreSQLJournalSpec(ITestOutputHelper outputHelper) : base(
            conf,
            "linq2dbJournalSpec",
            output: outputHelper)
        {
            var connFactory = new AkkaPersistenceDataConnectionFactory(
                new JournalConfig(
                    conf.GetConfig("akka.persistence.journal.testspec")));
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

            Initialize();

        }

        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
    }
}