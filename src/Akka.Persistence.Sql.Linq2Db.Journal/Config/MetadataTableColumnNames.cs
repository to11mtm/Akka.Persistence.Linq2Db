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
    sqlserver-compat-metadata-column-names {
        ""persistenceId"" = ""persistenceId""
        ""sequenceNumber"" = ""sequenceNr""
    }
    sqlite-compat-metadata-column-names {
        ""persistenceId"" = ""persistence_Id""
        ""sequenceNumber"" = ""sequence_nr""
    }
    postgres-compat-metadata-column-names {
        ""persistenceId"" = ""persistence_id""
        ""sequenceNumber"" = ""sequence_nr""
    }
}";
        public MetadataTableColumnNames(Configuration.Config config)
        {
            var compat = (config.GetString("table-compatibility-mode", "")??"").ToLower();
            string colString;
            switch (compat)
            {
                case "sqlserver":
                    colString = "sqlserver-compat-metadata-column-names";
                    break;
                case "sqlite":
                    colString = "sqlite-compat-metadata-column-names";
                    break;
                case "postgres":
                    colString = "postgres-compat-metadata-column-names";
                    break;
                default:
                    colString = "metadata-column-names";
                    break;
            }
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