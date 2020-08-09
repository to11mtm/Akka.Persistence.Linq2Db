using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class JournalTableConfiguration
    {
        public JournalTableConfiguration(Config config)
        {
            var localcfg = config.GetConfig("tables.journal").SafeWithFallback(Config.Empty);
            ColumnNames= new JournalTableColumnNames(config);
            TableName = localcfg.GetString("table-name", "journal");
            SchemaName = localcfg.GetString("schema-name", null);
            AutoInitialize = localcfg.GetBoolean("auto-init", false);
        }
        public JournalTableColumnNames ColumnNames { get; protected set; }
        public string TableName { get; protected set; }
        public string SchemaName { get; protected set; }
        public bool AutoInitialize { get; }
    }
}