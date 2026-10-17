#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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

        public static RentHandler<T> RentSegment<T>(this ArrayPool<T> pool, int amount)
        {
            var result = pool.Rent(amount);

            return new RentHandler<T>(pool, result, amount);
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

    public struct RentHandler<T> : IDisposable
    {
        public readonly ArraySegment<T> Values;
        private readonly ArrayPool<T> _pool;
        private T[] _result;
        private bool _disposed;

        internal RentHandler(ArrayPool<T> pool, T[] result, int amount)
        {
            _pool = pool;
            _result = result;

            Values = _result.Length == amount
                ? new ArraySegment<T>(_result)
                : new ArraySegment<T>(_result, offset: 0, count: amount);
            _disposed = false;
        }

        public readonly bool CanProvideArray([NotNullWhen(returnValue: true)] out T[]? array)
        {
            var condition = _result.Length == Values.Count && _disposed is false;

            array = condition ? _result : null;
            return condition;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _pool.Return(_result);
            _result = null!;
        }
    }
}
