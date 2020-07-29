using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DygBot
{
    public static class Extensions
    {
        public static T Random<T>(this IEnumerable<T> enumerable)
        {
            var rand = new Random();
            int index = rand.Next(0, enumerable.Count());
            return enumerable.ElementAt(index);
        }
        public static T Random<T>(this IEnumerable<T> enumerable, int seed)
        {
            var rand = new Random(seed);
            int index = rand.Next(0, enumerable.Count());
            return enumerable.ElementAt(index);
        }
    }
}
