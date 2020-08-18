using Akka.Configuration;
using LinqToDB.Data;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class BaseByteArrayJournalDaoConfig
    {
        public BaseByteArrayJournalDaoConfig(Config config)
        {
            
            BufferSize = config.GetInt("buffer-size", 30000);
            BatchSize = config.GetInt("batch-size", 2000);
            ReplayBatchSize = config.GetInt("replay-batch-size", 10000);
            Parallelism = config.GetInt("parallelism", 8);
            LogicalDelete = config.GetBoolean("logical-delete", true);
        }

        public int Parallelism { get; protected set; }

        public int BatchSize { get; protected set; }

        public bool LogicalDelete { get; protected set; }

        public int ReplayBatchSize { get; protected set; }

        public int BufferSize { get; protected set; }
        
    }
}