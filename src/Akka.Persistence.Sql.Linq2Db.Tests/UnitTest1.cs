using System;
using Akka.Util.Internal;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class UnitTest1 : Akka.Persistence.TCK.Journal.JournalSpec
    {
        private static AtomicCounter counter = new AtomicCounter(0);
        //private static string  connString = "FullUri=file:memdb"+counter.IncrementAndGet() +"?mode=memory&cache=shared";
        private static string connString =
            "Filename=file:memdb-journal-" + counter.IncrementAndGet() +
            ".db;Mode=Memory;Cache=Shared";
        private static SqliteConnection helSqLiteConnection =
            new SqliteConnection(connString);

        public UnitTest1(ITestOutputHelper outputHelper) : base(SQLiteJournalSpecConfig.Create(connString),
            "linq2dbJournalSpec",
            output: outputHelper)
        {
            try
            {
                helSqLiteConnection.Open();
            }
            catch{}
            DataConnection.OnTrace = info =>
            {
                outputHelper.WriteLine(info.SqlText);
                if (info.Exception != null)
                {
                    outputHelper.WriteLine(info.Exception.ToString());
                }

                if (!string.IsNullOrWhiteSpace(info.CommandText))
                {
                    outputHelper.WriteLine(info.CommandText);
                }
            };
            Initialize();
        }
        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
        protected override bool SupportsSerialization => false;
    }
}