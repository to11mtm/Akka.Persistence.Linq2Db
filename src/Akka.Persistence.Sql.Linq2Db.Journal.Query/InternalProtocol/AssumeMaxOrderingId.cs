namespace Akka.Persistence.Sql.Linq2Db.Journal.Query
{
    public class AssumeMaxOrderingId
    {
        public AssumeMaxOrderingId(long max)
        {
            Max = max;
        }

        public long Max { get; set; }
    }
}