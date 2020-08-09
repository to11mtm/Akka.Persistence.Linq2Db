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
        public AkkaPersistenceDataConnectionFactory(JournalConfig config)
        {
            var providerName = config.ProviderName;
            var connString = config.ConnectionString;
            var fmb = new MappingSchema(MappingSchema.Default)
                .GetFluentMappingBuilder();
            fmb.Entity<JournalRow>()
                .HasSchemaName(config.TableConfiguration.SchemaName)
                .HasTableName(config.TableConfiguration.TableName)
                .Member(r => r.deleted).HasColumnName(config
                    .TableConfiguration.ColumnNames.Deleted)
                .Member(r => r.manifest).HasColumnName(config
                    .TableConfiguration.ColumnNames.Manifest)
                .Member(r => r.message).HasColumnName(config
                    .TableConfiguration.ColumnNames.Message)
                .Member(r => r.ordering).HasColumnName(config
                    .TableConfiguration.ColumnNames.Ordering)
                .Member(r => r.tags)
                .HasColumnName(config.TableConfiguration.ColumnNames.Tags)
                .Member(r => r.Identifier).HasColumnName(config
                    .TableConfiguration.ColumnNames.Identitifer)
                .Member(r => r.persistenceId).HasColumnName(config
                    .TableConfiguration.ColumnNames.PersistenceId)
                .Member(r => r.sequenceNumber).HasColumnName(config
                    .TableConfiguration.ColumnNames.SequenceNumber);
            GetConnection = () =>
                new DataConnection(providerName, connString, fmb.MappingSchema);

        }

        public Func<DataConnection> GetConnection { get; set; }
    }
}