using System.Collections.Immutable;
using LanguageExt;
using LinqToDB.Async;

namespace Akka.Persistence.Sql.Linq2Db.Journal.Query
{
    public class EventsByTagResponseItem
    {
        public IPersistentRepresentation Repr { get; set; }
        public ImmutableHashSet<string> Tags { get; set; }
        public long SequenceNr { get; set; }
    }
}