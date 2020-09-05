using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Persistence.Journal;
using LanguageExt;
using static LanguageExt.Prelude;
using Seq = LanguageExt.Seq;

namespace Akka.Persistence.Sql.Linq2Db
{
    public abstract class PersistentReprSerializer<T>
    {
        public List<Util.Try<List<T>>> Serialize(
            IEnumerable<AtomicWrite> messages)
        {
            return messages.Select(aw =>
            {
                var serialized =
                    (aw.Payload as IEnumerable<IPersistentRepresentation>)
                    .Select(Serialize);
                return TrySeq.SequenceList(serialized);
            }).ToList();
        }
        
        public Seq<Util.Try<Seq<T>>> SerializeSeq(
            IEnumerable<AtomicWrite> messages)
        {
            return Seq(messages.Select(aw =>
            {
                var serialized =
                    (aw.Payload as IEnumerable<IPersistentRepresentation>)
                    .Select(Serialize);
                return TrySeq.SequenceSeq(serialized);
            }));
        }


        public Util.Try<T> Serialize(IPersistentRepresentation persistentRepr)
        {
            switch (persistentRepr.Payload)
            {
                case Tagged t:
                    return Serialize(persistentRepr.WithPayload(t.Payload), t.Tags);
                default:
                    return Serialize(persistentRepr,
                        ImmutableHashSet<string>.Empty);
            }
        }

        protected abstract Util.Try<T> Serialize(
            IPersistentRepresentation persistentRepr,
            IImmutableSet<string> tTags);

        protected abstract Util.Try<(IPersistentRepresentation, IImmutableSet<string>, long)>
            Deserialize(
                T t);
    }
}