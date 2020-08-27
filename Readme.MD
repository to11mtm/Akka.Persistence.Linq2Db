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

 - `parallelism` controls the number of Akka.Streams Queues used to write to the DB.
   - Default in JVM is `8`. We use `2`
     - For SQL Server, Based on testing `2` is a fairly optimal number in .NET and thusly chosen as the default. If you are on a constrained server with heavy traffic you may want to instead utilize a lower count.
       - Testing indicates that `2` will provide performance on par or better than both batching and non-batching journal.
     - For SQLite, you can (should) just put `1` here, because SQLite allows at most a single writer at a time even in WAL mode.

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
                        
                        parallelism = 4 # Number of Concurrent Writers

                        tables.journal { 
                                auto-init = true 
                                table-name = "journal"
                                schema-name = null
                                column-names {
                                       "ordering" = "ordering"
                                       "deleted" = "deleted"
                                       "persistenceId" = "persistenceId"
                                       "sequenceNumber" = "sequenceNumber"
                                       "created" = "created"
                                       "tags" = "tags"
                                       "message" = "message"
                                       "identifier" = "identifier"
                                       "manifest" = "manifest
                                      }
                               }
                    }
                }
            }
```