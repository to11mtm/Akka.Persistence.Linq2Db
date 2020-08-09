using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class SnapshotTableColumnNames
    {
        public SnapshotTableColumnNames(Config config)
        {
            var cfg =  config.GetConfig("tables.journal.column-names");
            PersistenceId = cfg.GetString("persistenceId", "persistence_id");
            SequenceNumber = cfg.GetString("sequenceNumber", "sequence_number");
            Created = cfg.GetString("created", "created");
            Snapshot = cfg.GetString("snapshot", "snapshot");
        }
        public string PersistenceId { get; }
        public string SequenceNumber { get; }
        public string Created { get; }
        public string Snapshot { get; }
    }
}