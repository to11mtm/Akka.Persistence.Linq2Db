using System;
using System.Collections.Generic;
using System.Text;
using Akka.Configuration;
using Akka.Persistence.Linq2Db.Journal.Query.Tests;
using Akka.Persistence.Query;
using Akka.Persistence.Sql.Linq2Db.Journal;
using Akka.Persistence.TCK.Query;
using Akka.Util.Internal;
using LinqToDB;
using Xunit.Abstractions;

namespace Akka.Persistence.Sqlite.Tests.Query
{
    /*
    public class SqliteCurrentAllEventsSpec:CurrentAllEventsSpec
    {
        public static readonly AtomicCounter Counter = new AtomicCounter(0);

        public static Config Config(int id)
        {
            var connectionString =
                $"Filename=file:memdb-journal-currentalleventsbytag-{id}.db;Mode=Memory;Cache=Shared";
            ConnectionContext.Remember(connectionString);
            return ConfigurationFactory.ParseString($@"
            akka.loglevel = INFO
            akka.persistence.journal.plugin = ""akka.persistence.journal.linq2db""
            akka.persistence.journal.linq2db {{
                plugin-dispatcher = ""akka.actor.default-dispatcher""
                table-name = event_journal
                metadata-table-name = journal_metadata
                auto-initialize = on
                provider-name = ""{ProviderName.SQLiteMS}""
                connection-string = ""{connectionString}""
                refresh-interval = 1s
                tables{{
                  journal{{
                       auto-init = true
                  }}
                }}
            }}
akka.persistence.query.journal.linq2db
            {{
                provider-name = ""{ProviderName.SQLiteMS}""
                connection-string = ""{connectionString}""
                table-name = event_journal
                metadata-table-name = journal_metadata
            }}
            akka.test.single-expect-default = 10s")
                .WithFallback(Linq2DbReadJournal.DefaultConfiguration)
                .WithFallback(Linq2DbWriteJournal.DefaultConfiguration);
        }

        public SqliteCurrentAllEventsSpec(ITestOutputHelper output) : base(Config(Counter.GetAndIncrement()), nameof(SqliteCurrentAllEventsSpec), output)
        {
            ReadJournal = Sys.ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);
        }

    }*/
}
