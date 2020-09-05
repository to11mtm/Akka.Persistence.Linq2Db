using System.Threading.Tasks;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Journal.DAO
{
    public interface IJournalDaoWithUpdates : IJournalDao
    {
        Task<Done> Update(string persistenceId, long sequenceNr,
            object payload);
    }
}