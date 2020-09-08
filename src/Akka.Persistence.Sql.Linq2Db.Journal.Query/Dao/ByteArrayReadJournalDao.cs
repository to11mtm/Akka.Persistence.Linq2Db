using Akka.Actor;
using Akka.Persistence.Sql.Linq2Db.Journal.Config;
using Akka.Persistence.Sql.Linq2Db.Journal.Types;
using Akka.Streams;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Query
{
    public class ByteArrayReadJournalDao : BaseByteReadArrayJournalDAO
    {
        public ByteArrayReadJournalDao(IAdvancedScheduler ec, IMaterializer mat, AkkaPersistenceDataConnectionFactory connectionFactory, ReadJournalConfig readJournalConfig, FlowPersistentReprSerializer<JournalRow> serializer) : base(ec, mat, connectionFactory, readJournalConfig, serializer)
        {
        }
    }
}