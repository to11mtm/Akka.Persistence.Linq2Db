﻿using System;
using System.Data;
using Akka.Persistence.Sql.Linq2Db.Tests.Performance;
using Akka.Persistence.TestKit.Performance;
using Akka.Util.Internal;
using LinqToDB;
using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class MSSQLiteLinq2DbJournalPerfSpec : L2dbJournalPerfSpec
    {
        private static AtomicCounter counter = new AtomicCounter(0);
        
        //private static string  connString = "FullUri=file:memdb"+counter.IncrementAndGet() +"?mode=memory&cache=shared";
        private static string connString =
            "Filename=file:memdb-journal-" + counter.IncrementAndGet() +
            ".db;Mode=Memory;Cache=Shared";

        private static SqliteConnection heldSqliteConnection =
            new SqliteConnection(connString);

        public static void InitWALForFileDb()
        {
            var c = new SqliteConnection(connString);
            c.Open();
            var walCommand = c.CreateCommand();
            walCommand.CommandText =
                @"
    PRAGMA journal_mode = 'wal'
";
            walCommand.ExecuteNonQuery();
        }
            
        public MSSQLiteLinq2DbJournalPerfSpec(ITestOutputHelper output)
            : base(SQLiteJournalSpecConfig.Create(connString, ProviderName.SQLiteMS), "SqliteJournalSpec", output)
        {
            
            heldSqliteConnection.Open();
            //InitWALForFileDb();
            var conf = new JournalConfig(
                SQLiteJournalSpecConfig.Create(connString, ProviderName.SQLiteMS).GetConfig("akka.persistence.journal.testspec"));
            
            var connFactory = new AkkaPersistenceDataConnectionFactory(conf);
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