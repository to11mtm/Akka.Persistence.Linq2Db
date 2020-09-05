using System;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Config
{
    public class ReadJournalConfig
    {
        public ReadJournalConfig(Configuration.Config config)
        {
            JournalTableConfig = new JournalTableConfig(config);
            JournalSequenceRetrievalConfiguration = new JournalSequenceRetrievalConfig(config);
            PluginConfig = new ReadJournalPluginConfig(config);
            RefreshInterval = config.GetTimeSpan("refresh-interval",
                TimeSpan.FromSeconds(1));
            MaxBufferSize = config.GetInt("max-buffer-size", 500);
            AddShutdownHook = config.GetBoolean("add-shutdown-hook", true);
            IncludeDeleted =
                config.GetBoolean("include-logically-deleted", true);
        }

        public int MaxBufferSize { get; set; }

        public bool AddShutdownHook { get; set; }

        public ReadJournalPluginConfig PluginConfig { get; set; }

        public TimeSpan RefreshInterval { get; set; }

        public JournalSequenceRetrievalConfig JournalSequenceRetrievalConfiguration { get; set; }

        public bool IncludeDeleted { get; set; }

        public JournalTableConfig JournalTableConfig { get;
            protected set;
        }
        
    }
}