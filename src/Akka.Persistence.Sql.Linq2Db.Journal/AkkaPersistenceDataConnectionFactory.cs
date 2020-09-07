using System;
using Akka.Persistence.Sql.Linq2Db.Journal.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Util;
using LinqToDB.Configuration;
using LinqToDB.Data;
using LinqToDB.Mapping;

namespace Akka.Persistence.Sql.Linq2Db.Journal
{
    public class AkkaPersistenceDataConnectionFactory
    {
        private string providerName;
        private string connString;
        private MappingSchema mappingSchema;
        private LinqToDbConnectionOptions opts;

        public AkkaPersistenceDataConnectionFactory(IProviderConfig config)
        {
            providerName = config.ProviderName;
            connString = config.ConnectionString;
            
            //Build Mapping Schema to be used for all connections.
            //Make a unique mapping schema name here to avoid problems
            //with multiple configurations using different schemas.
            var configName = "akka.persistence.l2db." + MurmurHash
                .ByteHash(Guid.NewGuid().ToByteArray()).ToString();
            var fmb = new MappingSchema(configName,MappingSchema.Default)
                .GetFluentMappingBuilder();
            var journalRowBuilder = fmb.Entity<JournalRow>()
                .HasSchemaName(config.TableConfig.SchemaName)
                .HasTableName(config.TableConfig.TableName)
                .Member(r => r.deleted).HasColumnName(config
                    .TableConfig.ColumnNames.Deleted)
                .Member(r => r.manifest).HasColumnName(config
                    .TableConfig.ColumnNames.Manifest).HasLength(500)
                .Member(r => r.message).HasColumnName(config
                    .TableConfig.ColumnNames.Message)
                .Member(r => r.ordering).HasColumnName(config
                    .TableConfig.ColumnNames.Ordering)
                .Member(r => r.tags).HasLength(100)
                .HasColumnName(config.TableConfig.ColumnNames.Tags)
                .Member(r => r.Identifier).HasColumnName(config
                    .TableConfig.ColumnNames.Identitifer)
                .Member(r => r.persistenceId).HasColumnName(config
                    .TableConfig.ColumnNames.PersistenceId).HasLength(255)
                .Member(r => r.sequenceNumber).HasColumnName(config
                    .TableConfig.ColumnNames.SequenceNumber)
                .Member(r=>r.Timestamp).HasColumnName(config.TableConfig.ColumnNames.Created);
            
            //Probably overkill, but we only set Metadata Mapping if specified
            //That we are in delete compatibility mode.
            if (config.IDaoConfig.DeleteCompatibilityMode)
            {
                fmb.Entity<JournalMetaData>().HasTableName(config.TableConfig.MetadataTableName)
                    .HasSchemaName(config.TableConfig.SchemaName)
                    .Member(r=>r.PersistenceId).HasColumnName(config.TableConfig.MetadataColumnNames.PersistenceId)
                    .HasLength(255)
                    .Member(r=>r.SequenceNumber).HasColumnName(config.TableConfig.MetadataColumnNames.SequenceNumber)
                    ;
            }

            useCloneDataConnection = config.UseCloneConnection;
            mappingSchema = fmb.MappingSchema;
            opts = new LinqToDbConnectionOptionsBuilder()
                .UseConnectionString(providerName, connString)
                .UseMappingSchema(mappingSchema).Build();
            _cloneConnection = new Lazy<DataConnection>(()=>new DataConnection(opts));
        }

        private Lazy<DataConnection> _cloneConnection;
        private bool useCloneDataConnection;

        public DataConnection GetConnection()
        {
            if (useCloneDataConnection)
            {
                return (DataConnection)_cloneConnection.Value.Clone();    
            }
            else
            {
                return new DataConnection(opts);    
            }
        }
    }
}