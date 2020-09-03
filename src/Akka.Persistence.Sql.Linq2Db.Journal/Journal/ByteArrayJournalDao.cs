using System;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Journal;
using Akka.Serialization;
using Akka.Streams;
using LinqToDB;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class ByteArrayJournalDao : BaseByteArrayJournalDao
    {
        public ByteArrayJournalDao(IAdvancedScheduler sched, IMaterializer mat,
            AkkaPersistenceDataConnectionFactory connection,
            JournalConfig journalConfig,
            Akka.Serialization.Serialization serializer) : base(sched, mat,
            connection, journalConfig,
            new ByteArrayJournalSerializer(journalConfig, serializer,
                journalConfig.PluginConfig.TagSeparator))
        {
        }

        public void InitializeTables()
        {
            using (var conn = _connectionFactory.GetConnection())
            {
                try
                {
                    conn.CreateTable<JournalRow>();
                }
                catch (Exception e)
                {
                    
                }

                if (_journalConfig.DaoConfig.DeleteCompatibilityMode)
                {
                    try
                    {
                        conn.CreateTable<JournalMetaData>();
                    }
                    catch (Exception e)
                    {

                    }
                }
            }
        }
    }
}