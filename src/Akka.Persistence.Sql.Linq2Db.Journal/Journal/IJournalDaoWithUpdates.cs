using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Linq2Db
{
    public interface IJournalDaoWithUpdates : IJournalDao
    {
        Task<Done> Update(string persistenceId, long sequenceNr,
            object payload);
    }
}