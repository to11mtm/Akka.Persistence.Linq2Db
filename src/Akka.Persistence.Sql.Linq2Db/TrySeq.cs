using System;
using System.Collections.Generic;
using System.Linq;
using LanguageExt;
using static LanguageExt.Prelude;
namespace Akka.Persistence.Sql.Linq2Db
{
    public static class TrySeq
    {
        public static Util.Try<IEnumerable<T>> Sequence<T>(IEnumerable<Util.Try<T>> seq) 
        {
            
                return Util.Try<IEnumerable<T>>.From(()=>seq.Select(r => r.Get()));
        }
        public static Util.Try<List<T>> SequenceList<T>(IEnumerable<Util.Try<T>> seq) 
        {
            return Util.Try<List<T>>.From(()=>seq.Select(r => r.Get()).ToList());
        }
        public static Util.Try<Seq<T>> SequenceSeq<T>(IEnumerable<Util.Try<T>> seq) 
        {
            return Util.Try<Seq<T>>.From(()=>seq.Select(r => r.Get()).ToList().ToSeq());
        }
    }
}