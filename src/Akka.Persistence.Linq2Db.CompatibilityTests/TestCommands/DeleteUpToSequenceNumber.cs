namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class DeleteUpToSequenceNumber
    {
        public DeleteUpToSequenceNumber(long nr)
        {
            Number = nr;
        }

        public long Number { get; }
    }
}