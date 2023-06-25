using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MusicTypePatcher
{
    internal static class Extensions
    {
        public static IEnumerable<T> Intersection<T>(this IEnumerable<T>? source, IEnumerable<T>? other, IEqualityComparer<T>? comparer = null)
        {
            var _comparer = comparer ?? EqualityComparer<T>.Default;

            if (source == null || other == null)
                yield break;

            var set = new HashSet<T>(source, _comparer);
            var lookup = other.ToLookup(u => u, _comparer);

            if (set.Count is 0 || lookup.Count is 0)
            {
                yield break;
            }

            foreach (var it in set)
            {
                var lookupCount = lookup[it].Count();
                var sourceCount = source.Count(u => _comparer.Equals(u, it));

                foreach (var item in Enumerable.Repeat(it, Math.Min(lookupCount, sourceCount)))
                {
                    yield return item;
                }
            }
        }

        public static IEnumerable<T> DisjunctLeft<T>(this IEnumerable<T> left, IEnumerable<T>? right, IEqualityComparer<T>? comparer = null) where T : notnull
        {
            _ = left ?? throw new ArgumentNullException(nameof(left));

            Dictionary<T, int> _dictionary = new(comparer);

            if (right != null)
            {
                foreach (var element in right)
                {
                    if (_dictionary.ContainsKey(element))
                    {
                        _dictionary[element]++;
                        continue;
                    }
                    _dictionary.Add(element, 1);
                }
            }

            foreach (var element in left)
            {
                if (_dictionary.TryGetValue(element, out var _count))
                {
                    switch (_count)
                    {
                        case <= 0:
                            yield return element;
                            break;
                        default:
                            _dictionary[element]--;
                            break;
                    }
                    continue;
                }
                yield return element;
            }
        }
    }
}
