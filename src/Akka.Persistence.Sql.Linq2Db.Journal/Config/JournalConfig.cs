namespace Akka.Persistence.Sql.Linq2Db.Journal.Config
{
    public class JournalConfig
    {
        public JournalConfig(Configuration.Config config)
        {
            ConnectionString = config.GetString("connection-string");
            ProviderName = config.GetString("provider-name");
            TableConfig = new JournalTableConfig(config);
            PluginConfig = new JournalPluginConfig(config);
            DaoConfig = new BaseByteArrayJournalDaoConfig(config);
            var dbConf = config.GetString(ConfigKeys.useSharedDb);
            UseSharedDb = string.IsNullOrWhiteSpace(dbConf) ? null : dbConf;
            UseCloneConnection =
                config.GetBoolean("use-clone-connection", false);
            
        }

        public string UseSharedDb { get; protected set; }

        public BaseByteArrayJournalDaoConfig DaoConfig { get; protected set; }

        public JournalPluginConfig PluginConfig { get; protected set; }

        public JournalTableConfig TableConfig { get;
            protected set;
        }

        public string DefaultSerializer { get; set; }
        public string ProviderName { get; }
        public string ConnectionString { get; }
        public bool UseCloneConnection { get; set; }
    }
}