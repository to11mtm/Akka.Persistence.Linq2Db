using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class MetadataTableColumnNames
    {
        public string FallBack = @"tables.journal.metadata-column-names {
}";
        public MetadataTableColumnNames(Config config)
        {
            var cfg =  config.GetConfig("tables.journal.metadata-column-names").SafeWithFallback(ConfigurationFactory.ParseString(FallBack).GetConfig("tables.journal.metadata-column-names"));
            PersistenceId =  cfg.GetString("persistenceId", "persistence_id");
            SequenceNumber = cfg.GetString("sequenceNumber", "sequence_number");
            }
        public string PersistenceId { get; }
        public string SequenceNumber { get; }
    }
    public class JournalTableColumnNames
    {
        public string FallBack = @"tables.journal.column-names {
}";
        public JournalTableColumnNames(Config config)
        {
            var cfg =  config.GetConfig("tables.journal.column-names").SafeWithFallback(ConfigurationFactory.ParseString(FallBack).GetConfig("tables.journal.column-names"));
            Ordering =       cfg.GetString("ordering","ordering");
            Deleted =        cfg.GetString("deleted","deleted");
            PersistenceId =  cfg.GetString("persistenceId", "persistence_id");
            SequenceNumber = cfg.GetString("sequenceNumber", "sequence_number");
            Created =        cfg.GetString("created", "timestamp");
            Tags =           cfg.GetString("tags", "tags");
            Message =        cfg.GetString("message", "message");
            Identitifer =    cfg.GetString("identifier", "identifier");
            Manifest =       cfg.GetString("manifest", "manifest");
        }
        public string Ordering { get; }
        public string Deleted { get; }
        public string PersistenceId { get; }
        public string SequenceNumber { get; }
        public string Created { get; }
        public string Tags { get; }
        public string Message { get; }
        public string Identitifer { get; }
        public string Manifest { get; }
    }
}