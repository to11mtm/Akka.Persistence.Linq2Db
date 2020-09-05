namespace Akka.Persistence.Sql.Linq2Db.Journal.Types
{
    public class ReplayCompletion
    {
        public IPersistentRepresentation repr { get; set; }
        public long SequenceNr { get; set; }
    }
}