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
            
            
            //seq.Select(r=>r.IsSuccess? r.Success.Value )
            if (seq.Any(t => t.IsSuccess == false))
            {
                return new Try<IEnumerable<T>>(seq
                    .FirstOrDefault(r => r.IsSuccess == false).Failure.Value);
            }
            else
            {
                return new Try<IEnumerable<T>>(seq.Select(r=>r.Success.Value));
            }
        }
    }
}