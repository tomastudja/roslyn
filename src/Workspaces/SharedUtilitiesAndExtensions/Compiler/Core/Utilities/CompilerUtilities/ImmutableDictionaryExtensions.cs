﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Roslyn.Utilities
{
    internal static class ImmutableDictionaryExtensions
    {
        public static ImmutableDictionary<K, ImmutableHashSet<V>> AddAll<K, V>(this ImmutableDictionary<K, ImmutableHashSet<V>> map, IEnumerable<K> keys, V value)
            => keys.Aggregate(map, (m, k) => m.Add(k, value));

        public static ImmutableDictionary<K, ImmutableHashSet<V>> Add<K, V>(this ImmutableDictionary<K, ImmutableHashSet<V>> map, K key, V value)
        {
            if (!map.TryGetValue(key, out var values))
            {
                values = ImmutableHashSet.Create<V>();
                return map.Add(key, values.Add(value));
            }

            return map.SetItem(key, values.Add(value));
        }

        public static ImmutableDictionary<K, ImmutableHashSet<V>> RemoveAll<K, V>(this ImmutableDictionary<K, ImmutableHashSet<V>> map, IEnumerable<K> keys, V value)
            => keys.Aggregate(map, (m, k) => m.Remove(k, value));

        public static ImmutableDictionary<K, ImmutableHashSet<V>> Remove<K, V>(this ImmutableDictionary<K, ImmutableHashSet<V>> map, K key, V value)
        {
            if (map.TryGetValue(key, out var values))
            {
                values = values.Remove(value);
                if (values.Count > 0)
                {
                    return map.SetItem(key, values);
                }
            }

            return map.Remove(key);
        }
    }
}
