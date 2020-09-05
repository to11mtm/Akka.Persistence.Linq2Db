using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Config
{
    public class MetadataTableColumnNames
    {
        public string FallBack = @"tables.journal.metadata-column-names {
}";
        public MetadataTableColumnNames(Configuration.Config config)
        {
            var cfg =  config.GetConfig("tables.journal.metadata-column-names").SafeWithFallback(ConfigurationFactory.ParseString(FallBack).GetConfig("tables.journal.metadata-column-names"));
            PersistenceId =  cfg.GetString("persistenceId", "persistenceId");
            SequenceNumber = cfg.GetString("sequenceNumber", "sequenceNr");
        }
        public string PersistenceId { get; }
        public string SequenceNumber { get; }
    }
}