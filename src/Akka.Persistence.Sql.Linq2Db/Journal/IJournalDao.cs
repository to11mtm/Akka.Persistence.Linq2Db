using System.Collections.Generic;
using System.Threading.Tasks;
using Akka.Util;

namespace Akka.Persistence.Sql.Linq2Db
{
    public interface IJournalDao : IJournalDaoWithReadMessages
    {
        
        Task Delete(string persistenceId, long toSequenceNr);
        Task<long> HighestSequenceNr(string persistenceId, long fromSequenceNr);

        Task<IEnumerable<Try<NotUsed>>> AsyncWriteMessages(
            IEnumerable<AtomicWrite> messages);
    }
}