using System;
using LinqToDB.Configuration;
using LinqToDB.Data;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class AkkaPersistenceDataConnectionFactory
    {
        private string providerName;
        private string connString;
        private MappingSchema mappingSchema;
        private LinqToDbConnectionOptions opts;

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
            opts = new LinqToDbConnectionOptionsBuilder()
                .UseConnectionString(providerName, connString)
                .UseMappingSchema(mappingSchema).Build();
            _cloneConnection = new Lazy<DataConnection>(()=>new DataConnection(opts));
        }

        private Lazy<DataConnection> _cloneConnection;
        
        public DataConnection GetConnection()
        {
            
            return (DataConnection)_cloneConnection.Value.Clone();
        }
    }
}