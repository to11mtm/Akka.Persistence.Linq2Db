using System;

namespace Akka.Persistence.Sql.Linq2Db.Snapshot
{
    public class SnapshotRow
    {
        public string persistenceId { get; set; }
        public long SequenceNumber { get; set; }
        public long Created { get; set; }
        public byte[] Payload { get; set; }
        public string Manifest { get; set; }
        public int SerializerId { get; set; }
    }
}