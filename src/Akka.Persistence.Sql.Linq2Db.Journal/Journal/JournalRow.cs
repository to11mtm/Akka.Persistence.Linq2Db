using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Linq2Db
{
    public sealed class JournalRow
    {
        [Identity]
        public long ordering { get; set; }

        public bool deleted { get; set; }
        [PrimaryKey]
        [NotNull]
        public string persistenceId { get; set; }
        [PrimaryKey]
        public long sequenceNumber { get; set; }
        public byte[] message { get; set; }
        public string tags { get; set; }
        public string manifest { get; set; }
        public int? Identifier { get; set; }
    }
}