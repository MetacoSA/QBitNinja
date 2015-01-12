using System;
using System.Collections.Generic;

namespace RapidBase
{
    public static class Extensions
    {
        public static T MinElement<T>(this IEnumerable<T> input, Func<T,int> predicate)
        {
            int min = int.MaxValue;
            T element = default(T);

            foreach (var el in input)
            {
                var val = predicate(el);
                if (val < min)
                {
                    min = predicate(el);
                    element = el;
                }
            }
            return element;
        }
    }
}
