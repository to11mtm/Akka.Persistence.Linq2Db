using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class SnapshotConfig
    {
        public SnapshotConfig(Config config)
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