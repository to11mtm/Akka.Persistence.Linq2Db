﻿using System;
using Akka.Configuration;
using Akka.Persistence.Sql.Linq2Db.Tests.Performance;
using JetBrains.dotMemoryUnit;
using LinqToDB;
using LinqToDB.Data;
using Xunit.Abstractions;

namespace Akka.Persistence.Sql.Linq2Db.Tests
{
    public class SqlServerJournalPerfSpec : L2dbJournalPerfSpec
    {
        public SqlServerJournalPerfSpec(ITestOutputHelper output) : base(InitConfig(),"sqlserverperfspec", output, eventsCount: 10000)
        {
            DotMemoryUnitTestOutput.SetOutputMethod(
                message => output.WriteLine(message));
            using (var conn =
                new DataConnection(ProviderName.SqlServer2008, ConnectionString.Instance.Replace("\\\\","\\")))
            {
                try
                {
                    conn.GetTable<JournalRow>().TableName("EventJournal").Delete();
                }
                catch (Exception e)
                {
                }
                
            }
        }
        public static Config InitConfig()
        {
            DbUtils.ConnectionString = ConnectionString.Instance;
            //need to make sure db is created before the tests start
            //DbUtils.Initialize(connString);
            var specString = $@"
                    akka.persistence {{
                        publish-plugin-commands = on
                        journal {{
                            plugin = ""akka.persistence.journal.sql-server""
                            sql-server {{
                                class = ""Akka.Persistence.SqlServer.Journal.SqlServerJournal, Akka.Persistence.SqlServer""
                                plugin-dispatcher = ""akka.persistence.dispatchers.default-plugin-dispatcher""
                                table-name = EventJournal
                                schema-name = dbo
                                auto-initialize = on
                                connection-string = ""{DbUtils.ConnectionString}""
                            }}
                        }}
                    }}";

            return ConfigurationFactory.ParseString(specString);
        }
    }
}