using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Config
{
    public class MetadataTableColumnNames
    {
        public string FallBack = @"tables.journal{
metadata-column-names {
          ""persistenceId"" = ""persistenceId""
        ""sequenceNumber"" = ""sequenceNr""
    }
    sqlite-compat-metadata-column-names {
    ""persistenceId"" = ""persistence_Id""
    ""sequenceNumber"" = ""sequence_nr""
    }
}";
        public MetadataTableColumnNames(Configuration.Config config)
        {
            var compat = config.GetString("table-compatibility-mode", "")??"";
            string colString;
            colString = compat.Equals("sqlserver",
                StringComparison.InvariantCultureIgnoreCase)
                ? "metadata-column-names"
                : compat.Equals("sqlite",
                    StringComparison.InvariantCultureIgnoreCase)
                    ? "sqlite-compat-metadata-column-names"
                    : "metadata-column-names";
            var cfg = config
                .GetConfig($"tables.journal.{colString}").SafeWithFallback(
                    ConfigurationFactory.ParseString(FallBack).GetConfig($"tables.journal.{colString}"));
            //var cfg =  config.GetConfig("tables.journal.metadata-column-names").SafeWithFallback(ConfigurationFactory.ParseString(FallBack).GetConfig("tables.journal.metadata-column-names"));
            PersistenceId =  cfg.GetString("persistenceId", "persistenceId");
            SequenceNumber = cfg.GetString("sequenceNumber", "sequenceNr");
        }
        public string PersistenceId { get; }
        public string SequenceNumber { get; }
    }
}