namespace Akka.Persistence.Sql.Linq2Db.Config
{
    public class SnapshotConfig
    {
        public SnapshotConfig(Configuration.Config config)
        {
            SnapshotTableConfiguration = new SnapshotTableConfiguration(config);
            PluginConfig = new SnapshotPluginConfig(config);
            var dbConf = config.GetString(ConfigKeys.useSharedDb);
            UseSharedDb = string.IsNullOrWhiteSpace(dbConf) ? null : dbConf;
        }

        public string UseSharedDb { get; protected set; }

        public SnapshotPluginConfig PluginConfig { get; protected set; }

        public SnapshotTableConfiguration SnapshotTableConfiguration { get;
            protected set;
        }
    }
}