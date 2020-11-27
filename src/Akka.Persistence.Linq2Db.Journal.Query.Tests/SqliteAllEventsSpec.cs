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
    public class SqliteAllEventsSpec:AllEventsSpec
    {
        
        public static Config Config
        {
            get
            {
            var  runGuid = Guid.NewGuid();
            var connString =
                $"Filename=file:memdb-journal-allevents-{runGuid}.db;Mode=Memory;Cache=Shared";
            ConnectionContext.Remember(connString);
                return ConfigurationFactory.ParseString($@"
            akka.loglevel = INFO
            akka.persistence.journal.plugin = ""akka.persistence.journal.linq2db""
            akka.persistence.journal.linq2db {{
                plugin-dispatcher = ""akka.actor.default-dispatcher""
                table-name = event_journal
                provider-name = ""{ProviderName.SQLiteMS}""
                metadata-table-name = journal_metadata
                tables{{
                  journal{{
                       auto-init = true
                  }}
                }}
                connection-string = ""{connString}""
                refresh-interval = 1s
            }}
            akka.persistence.query.journal.linq2db
            {{
                provider-name = ""{ProviderName.SQLiteMS}""
                connection-string = ""{connString}""
                table-name = event_journal
                metadata-table-name = journal_metadata
                write-plugin = ""akka.persistence.journal.linq2db""
            }}
            akka.test.single-expect-default = 10s")
                    .WithFallback(Linq2DbReadJournal.DefaultConfiguration)
                    .WithFallback(Linq2DbWriteJournal.DefaultConfiguration);
            }
        }

        public SqliteAllEventsSpec(ITestOutputHelper output) : base(Config, nameof(SqliteAllEventsSpec), output)
        {
            ReadJournal = Sys.ReadJournalFor<Linq2DbReadJournal>(Linq2DbReadJournal.Identifier);
        }

    }*/
}
