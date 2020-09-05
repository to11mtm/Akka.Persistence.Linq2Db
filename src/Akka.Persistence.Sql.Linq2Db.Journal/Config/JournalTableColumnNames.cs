using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Config
{
    public class JournalTableColumnNames
    {
        public string FallBack = @"tables.journal
  { 
    compat-column-names {
                ""ordering"" = ""ordering""
            ""deleted"" = ""isdeleted""
            ""persistenceId"" = ""persistenceId""
            ""sequenceNumber"" = ""sequenceNr""
            ""created"" = ""timestamp""
            ""tags"" = ""tags""
            ""message"" = ""payload""
            ""identifier"" = ""serializerid""
            ""manifest"" = ""manifest""
}
 column-names
 { 
 }
}";
        public JournalTableColumnNames(Configuration.Config config)
        {
            var compat = config.GetBoolean("table-compatibility-mode", false);
            var cfg = config
                .GetConfig(compat
                    ? "tables.journal.compat-column-names"
                    : "tables.journal.column-names").SafeWithFallback(
                    ConfigurationFactory.ParseString(FallBack).GetConfig(compat
                        ? "tables.journal.compat-column-names"
                        : "tables.journal.column-names"));
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