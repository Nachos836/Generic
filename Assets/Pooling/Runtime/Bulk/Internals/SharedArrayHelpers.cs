#nullable enable

using System;
using System.Buffers;

namespace Pooling.Bulk.Internals
{
    internal static class SharedArrayHelpers
    {
        public static T[] RentWithClear<T>(this ArrayPool<T> pool, int amount, T defaultValue)
        {
            var result = pool.Rent(amount);
            if (amount == result.Length) return result;

            var leftovers = result.AsSpan()[amount .. ];
            leftovers.Fill(defaultValue);

            return result;
        }

        public static T[] RentWithClear<T>(this ArrayPool<T> pool, int amount)
        {
            var result = pool.Rent(amount);
            if (amount == result.Length) return result;

            var leftovers = result.AsSpan()[amount .. ];
            leftovers.Clear();

            return result;
        }
    }
}
