namespace Akka.Persistence.Sql.Linq2Db.Config
{
    
    public class SnapshotTableConfiguration
    {
        public SnapshotTableConfiguration(Configuration.Config config)
        {
            var localcfg = config.GetConfig("tables.snapshot");
            ColumnNames= new SnapshotTableColumnNames(config);
            TableName = localcfg.GetString("table-name", "snapshot");
            SchemaName = localcfg.GetString("schema-name", null);
        }
        public SnapshotTableColumnNames ColumnNames { get; protected set; }
        public string TableName { get; protected set; }
        public string SchemaName { get; protected set; }
    }
}