# Akka.Persistence.Linq2Db

A Cross-SQL-DBA Akka.Persistence plugin

#### This is still a WORK IN PROGRESS

 **Pull Requests are Welcome** but please note this is still considered 'work in progress' and only used if one understands the risks. While the TCK Specs pass you should still test in a 'safe' non-production environment carefully before deciding to fully deploy.
 
 Additionally, at this time there is no migration or backward compatibility with past Akka.Persistence SQL providers. The columns themselves should be a good 'fit' with the existing designs for SQLite/SQLServer but full compatibility should not be considered a guarantee (particularly in SQLite due to possible differences in how data is stored). 

## Currently Implemented:

- Journal
  - With `JournalSpec` and `JournalPerfSpec` passing for MS SQL Server and Microsoft.Data.SQLite
- Configuration
  - No unit tests yet.
  - Still Lacking Default Configs.

## Incomplete:

- SnapshotStore
 
## Configuration Samples:

### Journal:

Please note that you -must- provide a Connection String and Provider name.

Refer to the Members of `Linq2Db.ProviderName` for included providers.



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