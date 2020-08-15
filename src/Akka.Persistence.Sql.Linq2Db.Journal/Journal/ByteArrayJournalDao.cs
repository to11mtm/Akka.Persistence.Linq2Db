using System;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Journal;
using Akka.Serialization;
using Akka.Streams;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.Mapping;

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
            }
        }
    }


    public class AkkaPersistenceDataConnectionFactory
    {
        private string providerName;
        private string connString;
        private MappingSchema mappingSchema;
        public AkkaPersistenceDataConnectionFactory(JournalConfig config)
        {
            providerName = config.ProviderName;
            connString = config.ConnectionString;
            var fmb = new MappingSchema(MappingSchema.Default)
                .GetFluentMappingBuilder();
            fmb.Entity<JournalRow>()
                .HasSchemaName(config.TableConfiguration.SchemaName)
                .HasTableName(config.TableConfiguration.TableName)
                .Member(r => r.deleted).HasColumnName(config
                    .TableConfiguration.ColumnNames.Deleted)
                .Member(r => r.manifest).HasColumnName(config
                    .TableConfiguration.ColumnNames.Manifest).HasLength(500)
                .Member(r => r.message).HasColumnName(config
                    .TableConfiguration.ColumnNames.Message)
                .Member(r => r.ordering).HasColumnName(config
                    .TableConfiguration.ColumnNames.Ordering)
                .Member(r => r.tags).HasLength(100)
                .HasColumnName(config.TableConfiguration.ColumnNames.Tags)
                .Member(r => r.Identifier).HasColumnName(config
                    .TableConfiguration.ColumnNames.Identitifer)
                .Member(r => r.persistenceId).HasColumnName(config
                    .TableConfiguration.ColumnNames.PersistenceId).HasLength(255)
                .Member(r => r.sequenceNumber).HasColumnName(config
                    .TableConfiguration.ColumnNames.SequenceNumber);
            mappingSchema = fmb.MappingSchema;

        }

        public DataConnection GetConnection()
        {
            return new DataConnection(providerName,connString,mappingSchema);
        }
    }
}