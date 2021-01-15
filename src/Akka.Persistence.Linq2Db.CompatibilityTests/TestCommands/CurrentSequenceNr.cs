namespace Akka.Persistence.Linq2Db.CompatibilityTests
{
    public class CurrentSequenceNr
    {
        public CurrentSequenceNr(long sn)
        {
            SequenceNumber = sn;
        }

        public long SequenceNumber { get; }
    }
}