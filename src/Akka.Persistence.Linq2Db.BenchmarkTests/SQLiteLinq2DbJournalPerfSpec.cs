using System;
using Akka.Persistence.Sql.Linq2Db.Tests.Performance;
using Akka.Persistence.TestKit.Performance;
using Akka.Util.Internal;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class SQLiteLinq2DbJournalPerfSpec : L2dbJournalPerfSpec
    {
        private static AtomicCounter counter = new AtomicCounter(0);
        
        //private static string  connString = "FullUri=file:memdb"+counter.IncrementAndGet() +"?mode=memory&cache=shared";
        private static string connString =
            "Filename=file:memdb-journal-" + counter.IncrementAndGet() +
            ".db;Mode=Memory;Cache=Shared";

        private static Lazy<SqliteConnection> helSqLiteConnection = new Lazy<SqliteConnection>(
            () =>
            {
                var c = new SqliteConnection(connString);
                c.Open();
                var walCommand = c.CreateCommand();
                walCommand.CommandText =
                    @"
    PRAGMA journal_mode = 'wal'
";
                walCommand.ExecuteNonQuery();
                return c;
            });
            
        public SQLiteLinq2DbJournalPerfSpec(ITestOutputHelper output)
            : base(SQLiteJournalSpecConfig.Create(helSqLiteConnection.Value.ConnectionString), "SqliteJournalSpec", output)
        {
        }
        
    }
}