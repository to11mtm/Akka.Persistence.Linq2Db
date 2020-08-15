using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Util;

namespace Akka.Persistence.Sql.Linq2Db
{
    public static class TrySeq
    {
        public static Try<IEnumerable<T>> Sequence<T>(IEnumerable<Try<T>> seq) 
        {
            
                return Try<IEnumerable<T>>.From(()=>seq.Select(r => r.Get()));
        }
        public static Try<List<T>> SequenceList<T>(IEnumerable<Try<T>> seq) 
        {
            return Try<List<T>>.From(()=>seq.Select(r => r.Get()).ToList());
        }
    }
}