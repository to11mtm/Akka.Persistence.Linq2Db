using Akka.Configuration;
using LinqToDB.Data;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class BaseByteArrayJournalDaoConfig
    {
        public BaseByteArrayJournalDaoConfig(Config config)
        {
            
            BufferSize = config.GetInt("buffer-size", 1000);
            BatchSize = config.GetInt("batch-size", 2000);
            ReplayBatchSize = config.GetInt("replay-batch-size", 1000);
            Parallelism = config.GetInt("parallelism", 2);
            LogicalDelete = config.GetBoolean("logical-delete", true);
            MaxRowByRowSize = config.GetInt("max-row-by-row-size", 100);
            DeleteCompatibilityMode =
                config.GetBoolean("delete-compatibility-mode", true);
        }

        /// <summary>
        /// Specifies the batch size at which point <see cref="BulkCopyType"/>
        /// will switch to 'Default' instead of 'MultipleRows'. For smaller sets
        /// (i.e. 100 entries or less) the cost of Bulk copy setup for DB may be worse.
        /// </summary>
        public int MaxRowByRowSize { get; set; }

        public int Parallelism { get; protected set; }

        public int BatchSize { get; protected set; }

        public bool LogicalDelete { get; protected set; }

        public int ReplayBatchSize { get; protected set; }

        public int BufferSize { get; protected set; }
        
        public bool DeleteCompatibilityMode { get; protected set; }
        
    }
}