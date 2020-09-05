# Akka.Persistence.Linq2Db

A Cross-SQL-DBA Akka.Persistence plugin with broad database compatibility thanks to Linq2Db.

This is a Fairly-Naive port of the amazing akka-persistence-jdbc package from Scala to C#. 


#### This is still a WORK IN PROGRESS

 **Pull Requests are Welcome** but please note this is still considered 'work in progress' and only used if one understands the risks. While the TCK Specs pass you should still test in a 'safe' non-production environment carefully before deciding to fully deploy.
 
 Additionally, at this time there is no migration or backward compatibility with past Akka.Persistence SQL providers. The columns themselves should be a good 'fit' with the existing designs for SQLite/SQLServer but full compatibility should not be considered a guarantee (particularly in SQLite due to possible differences in how data is stored). 

## Advantages over `Akka.Contrib.Persistence.Sql.Common` Implementations:

 - Akka.Streams used aggressively for tune-able blocking overhead.
 
 - Linq2Db usage for easier swapping of backend DBs.

## Currently Implemented:

- Journal
  - With `JournalSpec` and `JournalPerfSpec` passing for MS SQL Server and Microsoft.Data.SQLite
- Configuration
  - No unit tests yet.
  - Still Lacking Default Configs.

## Incomplete:

- SnapshotStore
- Journal Queries (i.e. Tag queries. Tags are properly persisted at this time.)
 
## Configuration Samples:

### Journal:

Please note that you -must- provide a Connection String and Provider name.

 - Refer to the Members of `Linq2Db.ProviderName` for included providers.
   - Note: For best performance, one should use the most specific provider name possible. i.e. `LinqToDB.ProviderName.SqlServer2012` instead of `LinqToDB.ProviderName.SqlServer`. Otherwise certain provider detections have to run more frequently which may impair performance slightly. 

 - `parallelism` controls the number of Akka.Streams Queues used to write to the DB.
   - Default in JVM is `8`. We use `2`
     - For SQL Server, Based on testing `2` is a fairly optimal number in .NET and thusly chosen as the default. You may wish to adjust up if you are dealing with a large number of actors.
       - Testing indicates that `2` will provide performance on par or better than both batching and non-batching journal.
     - For SQLite, you may want to just put `1` here, because SQLite allows at most a single writer at a time even in WAL mode.
       - Keep in mind there may be some latency/throughput trade-offs if your write-set gets large.
   - Note that these run on the threadpool, not on dedicated threads. Setting this number too high may steal work from other actors.
     - It's worth noting that LinqToDb's Bulk Copy implementations are -very- efficient here, since under many DBs the batch can be done in a single round-trip.
 - `logical-delete` if `true` will only set the deleted flag for items, i.e. will not actually delete records from DB.
   - if `false` all records are set as deleted, and then all but the top record is deleted. This top record is used for sequence number tracking in case no other records exist in the table.
 - `delete-compatibility-mode` specifies to perform deletes in a way that is compatible with Akka.Persistence.Sql.Common.
   - This will use a Journal_Metadata table (or otherwise defined )
   - Note that this setting is independent of `logical-delete`
 - `use-clone-connection` is a bit of a hack. Currently Linq2Db has a performance penalty for custom mapping schemas. Cloning the connection is faster but may not work for all scenarios.
   - tl;dr - If a password or similar is in the connection string, leave `use-clone-connection` set to `false`.
   - If you don't have a password or similar, run some tests with it set to `true`. You'll see improved write and read performance.
 - For Table Configuration:
   - Note that Tables/Columns will be created with the casing provided, and selected in the same way (i.e. if using a DB with case sensitive columns, be careful!)
   - The 'created/timestamp' column is not actually used. Because of the nature of batched writes (and lack of guidance from jdbc library, feature appears to not be implemented there) we always use 0.
     - The column was kept to maximize compatibility with Existing SQL Journals.

```
akka.persistence {
                publish-plugin-commands = on
                journal {
                    plugin = "akka.persistence.journal.testspec"
                    testspec {
                        class = "Akka.Persistence.Sql.Linq2Db.Linq2DbWriteJournal, Akka.Persistence.Sql.Linq2Db"
                        plugin-dispatcher = "akka.actor.default-dispatcher"
                        connection-string = "" # Connection String is Required! 
                        provider-name = "" # Provider name is required#
                        delete-compatibility-mode = true # If true, journal_metadata is created

                        # If true, default column names are compatible with
                        # Akka.Persistence.Sql Default Column names.                       
                        table-compatibility-mode = false 
                        
  
                        parallelism = 3 # Number of Concurrent Writers

                        tables.journal { 
                                
                                #if delete-compatibility-mode is true, both tables are created
                                #if false, only journal table will be created.
                                auto-init = true
 
                                table-name = "journal"
                                metadata-table-name = "journal_metadata"
                                
                                #If you want to specify a schema for your tables, you can do so here.
                                schema-name = null

                                #Only set to TRUE if unit tests pass with the connection string you intend to use!
                                use-clone-connection = false

                                #Note COMPAT: denotes value to be used for compatibility
                                column-names {
                                       "ordering" = "ordering"
                                       "deleted" = "deleted"  #COMPAT: "isdeleted"
                                       "persistenceId" = "persistenceId"
                                       "sequenceNumber" = "sequenceNumber" #COMPAT: "sequenceNr"
                                       "created" = "created" #COMPAT:"Timestamp"
                                       "tags" = "tags"
                                       "message" = "message"
                                       "identifier" = "identifier" #COMPAT: "serializerid"
                                       "manifest" = "manifest"
                                      }
                                compat-column-names {
                                       "ordering" = "ordering"
                                       "deleted" = "isdeleted"
                                       "persistenceId" = "persistenceId"
                                       "sequenceNumber" = "sequenceNr"
                                       "created" = "Timestamp"
                                       "tags" = "tags"
                                       "message" = "message"
                                       "identifier" = "serializerid"
                                       "manifest" = "manifest"
                                }
                               }
                    }
                }
            }
```