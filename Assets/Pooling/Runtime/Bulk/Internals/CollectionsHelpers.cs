#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using Object = UnityEngine.Object;

namespace Pooling.Bulk.Internals
{
    public static class CollectionsHelpers
    {
        public static List<Object> GetObjectList<T>(this List<T> income) where T : Object => Unsafe.As<List<T>, List<Object>>(ref income);

        public static NativeArray<T> SliceAsNativeArray<T>(this NativeList<T> list, Range range) where T : unmanaged
        {
            var view = list.AsSpan()[range];
            var array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray(view, Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            AtomicSafetyHandle.UseSecondaryVersion(ref list.m_Safety);
            NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, list.m_Safety);
#endif

            return array;
        }

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
