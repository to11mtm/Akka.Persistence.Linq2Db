namespace Akka.Persistence.Sql.Linq2Db.Journal.Query
{
    public class ScheduleAssumeMaxOrderingId
    {
        public ScheduleAssumeMaxOrderingId(long maxInDatabase)
        {
            MaxInDatabase = maxInDatabase;
        }

        public long MaxInDatabase { get; set; }
    }
}