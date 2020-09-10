namespace Akka.Persistence.Sql.Linq2Db.Journal.Query
{
    public class MaxOrderingId
    {
        public MaxOrderingId(long max)
        {
            Max = max;
        }

        public long Max { get; set; }
    }
}