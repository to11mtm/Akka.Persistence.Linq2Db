﻿using System;
using Akka.Configuration;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class JournalSequenceRetrievalConfig
    {
        public JournalSequenceRetrievalConfig(Config config)
        {
            BatchSize = config.GetInt("journal-sequence-retrieval.batch-size",
                10000);
            MaxTries =
                config.GetInt("journal-sequence-retrieval.max-tries", 10);
            QueryDelay = config.GetTimeSpan(
                "journal-sequence-retrieval.query-delay",
                TimeSpan.FromSeconds(1));
            MaxBackoffQueryDelay = config.GetTimeSpan(
                "journal-sequence-retrieval.max-backoff-query-delay",
                TimeSpan.FromSeconds(60));
            AskTimeout = config.GetTimeSpan(
                "journal-sequence-retrieval.ask-timeout",
                TimeSpan.FromSeconds(1));
        }

        public TimeSpan AskTimeout { get; set; }

        public TimeSpan MaxBackoffQueryDelay { get; set; }

        public TimeSpan QueryDelay { get; set; }

        public int MaxTries { get; set; }

        public int BatchSize { get; set; }

        public static JournalSequenceRetrievalConfig Apply(Config config)
        {
            return new JournalSequenceRetrievalConfig(config);   
        }
        
    }
}