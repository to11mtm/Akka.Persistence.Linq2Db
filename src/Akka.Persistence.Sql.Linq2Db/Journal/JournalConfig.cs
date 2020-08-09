using Akka.Configuration;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class JournalConfig
    {
        public JournalConfig(Config config)
        {
            ConnectionString = config.GetString("connection-string");
            ProviderName = config.GetString("provider-name");
            TableConfiguration = new JournalTableConfiguration(config);
            PluginConfig = new JournalPluginConfig(config);
            DaoConfig = new BaseByteArrayJournalDaoConfig(config);
            var dbConf = config.GetString(ConfigKeys.useSharedDb);
            UseSharedDb = string.IsNullOrWhiteSpace(dbConf) ? null : dbConf;
        }

        public string UseSharedDb { get; protected set; }

        public BaseByteArrayJournalDaoConfig DaoConfig { get; protected set; }

        public JournalPluginConfig PluginConfig { get; protected set; }

        public JournalTableConfiguration TableConfiguration { get;
            protected set;
        }

        public string DefaultSerializer { get; set; }
        public string ProviderName { get; }
        public string ConnectionString { get; }
    }
}