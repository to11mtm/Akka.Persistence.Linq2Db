using System.Collections.Immutable;
using System.Threading.Tasks;
using Akka.Persistence.Sql.Linq2Db.Journal.DAO;
using Akka.Streams.Dsl;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Query
{
    public interface IReadJournalDAO : IJournalDaoWithReadMessages
    {
        Source<string, NotUsed> allPersistenceIdsSource(long max);

        Source<Util.Try<(IPersistentRepresentation, IImmutableSet<string>, long)>, NotUsed>
            eventsByTag(string tag, long offset, long maxOffset, long max);

        Source<long, NotUsed> journalSequence(long offset,
            long limit);

        Task<long> maxJournalSequence();
    }
}