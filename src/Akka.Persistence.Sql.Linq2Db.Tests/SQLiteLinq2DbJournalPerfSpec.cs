using Akka.Persistence.TestKit.Performance;
using Akka.Util.Internal;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class SQLiteLinq2DbJournalPerfSpec : JournalPerfSpec
    {
        private static AtomicCounter counter = new AtomicCounter(0);
        
        //private static string  connString = "FullUri=file:memdb"+counter.IncrementAndGet() +"?mode=memory&cache=shared";
        private static string connString =
            "Filename=file:memdb-journal-" + counter.IncrementAndGet() +
            ".db;Mode=Memory;Cache=Shared";

        private static SqliteConnection helSqLiteConnection =
            new SqliteConnection(connString);
        public SQLiteLinq2DbJournalPerfSpec(ITestOutputHelper output)
            : base(SQLiteJournalSpecConfig.Create(connString), "SqliteJournalSpec", output)
        {
            try
            {
                helSqLiteConnection.Open();
            }
            catch{}
        }
        
    }
}