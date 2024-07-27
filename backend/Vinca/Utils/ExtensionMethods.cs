using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vinca.Utils
{
    public static class StringExtensions
    {
        public static string StringJoin<T>(this IEnumerable<T> enumerable, string separator, Func<T, string> propSelector)
        {
            if (enumerable == null || !enumerable.Any()) return string.Empty;

            return string.Join(separator, enumerable.Select(t => propSelector(t)));
        }

        public static string StringJoin<T>(this IEnumerable<T> enumerable, string separator)
        {
            return StringJoin(enumerable, separator, t => t?.ToString() ?? string.Empty);
        }
    }
}
