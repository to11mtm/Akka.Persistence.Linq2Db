using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class JournalTableConfiguration
    {
        public JournalTableConfiguration(Config config)
        {
            var localcfg = config.GetConfig("tables.journal").SafeWithFallback(Config.Empty);
            ColumnNames= new JournalTableColumnNames(config);
            MetadataColumnNames = new MetadataTableColumnNames(config);
            TableName = localcfg.GetString("table-name", "journal");
            MetadataTableName = localcfg.GetString("metadata-table-name",
                "journal_metadata");
            SchemaName = localcfg.GetString("schema-name", null);
            AutoInitialize = localcfg.GetBoolean("auto-init", false);
        }
        public JournalTableColumnNames ColumnNames { get; protected set; }
        public string TableName { get; protected set; }
        public string SchemaName { get; protected set; }
        public bool AutoInitialize { get; }
        public string MetadataTableName { get; set; }
        public MetadataTableColumnNames MetadataColumnNames { get; set; }
    }
}