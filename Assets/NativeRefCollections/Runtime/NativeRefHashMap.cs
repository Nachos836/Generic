using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static System.Runtime.CompilerServices.MethodImplOptions;

namespace NativeRefCollections.Runtime
{
    using CollectionsBridge;

    [NativeContainer]
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct NativeRefHashMap<TKey, TValue> : INativeDisposable
        where TKey : unmanaged, IEquatable<TKey>
        where TValue : unmanaged
    {
        private const int MaxCapacity = HashMapHelperAccess<TKey>.MaxCapacity;

        internal HashMapHelperAccess<TKey> Data;

        public NativeRefHashMap(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            Data = default;
            Data.Init(initialCapacity, allocator);
        }

        public void Dispose() => Data.Dispose();

        public JobHandle Dispose(JobHandle inputDeps) => Data.Dispose(inputDeps);

        public readonly bool IsCreated
        {
            [MethodImpl(AggressiveInlining)] get => Data.IsCreated;
        }

        public readonly bool IsEmpty
        {
            [MethodImpl(AggressiveInlining)] get => Data.IsEmpty;
        }

        public readonly int Count
        {
            [MethodImpl(AggressiveInlining)] get => Data.Count;
        }

        public int Capacity
        {
            [MethodImpl(AggressiveInlining)] readonly get => Data.Capacity;
            [MethodImpl(AggressiveInlining)] set => Data.Capacity = value;
        }

        [MethodImpl(AggressiveInlining)]
        public void Clear() => Data.Clear();

        [MethodImpl(AggressiveInlining)]
        public bool TryAdd(in TKey key, in TValue item)
        {
            var idx = Data.TryAdd(in key);
            if (-1 == idx) return false;

            UnsafeUtility.WriteArrayElement(Data.Ptr, idx, item);

            return true;
        }

        [MethodImpl(AggressiveInlining)]
        public void Add(in TKey key, in TValue item)
        {
            var idx = Data.TryAdd(in key);
            if (-1 != idx)
            {
                UnsafeUtility.WriteArrayElement(Data.Ptr, idx, item);
                return;
            }

            if (Capacity == MaxCapacity)
            {
                ThrowAtMaxCapacity();
                return;
            }

            ThrowKeyAlreadyAdded(key);
        }

        [MethodImpl(AggressiveInlining)]
        public bool Remove(in TKey key) => -1 != Data.TryRemove(in key);

        [MethodImpl(AggressiveInlining)]
        public readonly bool TryGetValue(in TKey key, out TValue item) => Data.TryGetValue(in key, out item);

        [MethodImpl(AggressiveInlining)]
        public readonly bool ContainsKey(in TKey key) => Data.Contains(key);

        [MethodImpl(AggressiveInlining)]
        public void TrimExcess() => Data.TrimExcess();

        [MethodImpl(AggressiveInlining)]
        public void SetValue(in TKey key, in TValue value)
        {
            var idx = Data.Find(key);
            if (-1 == idx)
            {
                TryAdd(key, value);
                return;
            }

            UnsafeUtility.WriteArrayElement(Data.Ptr, idx, value);
        }

        public readonly ref TValue this[in TKey key]
        {
            [MethodImpl(AggressiveInlining)] get
            {
                ref var result = ref Data.TryGetRefValue<TValue>(in key);
                if (Unsafe.IsNullRef(ref result))
                {
                    ThrowKeyNotPresent(key);
                }

                return ref result;
            }
        }

        /// <summary>
        /// Returns an enumerator over the key-value pairs of this hash map.
        /// </summary>
        /// <returns>An enumerator over the key-value pairs of this hash map.</returns>
        public Enumerator GetEnumerator()
        {
            fixed (HashMapHelperAccess<TKey>* access = &Data)
            {
                return new Enumerator { EnumeratorHelper = new HashMapHelperAccess<TKey>.Enumerator(access) };
            }
        }

        /// <summary>
        /// An enumerator over the key-value pairs of a container.
        /// </summary>
        /// <remarks>
        /// In an enumerator's initial state, <see cref="Current"/> is not valid to read.
        /// From this state, the first <see cref="MoveNext"/> call advances the enumerator to the first key-value pair.
        /// </remarks>
        [NativeContainer]
        [NativeContainerIsReadOnly]
        public struct Enumerator
        {
            [NativeDisableUnsafePtrRestriction]
            internal HashMapHelperAccess<TKey>.Enumerator EnumeratorHelper;

            [MethodImpl(AggressiveInlining)]
            public bool MoveNext() => EnumeratorHelper.MoveNext();

            public void Reset() => EnumeratorHelper.Reset();

            public readonly KVPair<TKey, TValue> Current
            {
                [MethodImpl(AggressiveInlining)] get => EnumeratorHelper.GetCurrent<TValue>();
            }
        }

        public ReadOnly AsReadOnly()
        {
            fixed (HashMapHelperAccess<TKey>* access = &Data)
            {
                return new ReadOnly(access);
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        public readonly struct ReadOnly
        {
            [NativeDisableUnsafePtrRestriction]
            private readonly HashMapHelperAccess<TKey>* _access;

            internal ReadOnly(HashMapHelperAccess<TKey>* access)
            {
                _access = access;
            }

            public bool IsCreated { [MethodImpl(AggressiveInlining)] get => _access->IsCreated; }
            public bool IsEmpty { [MethodImpl(AggressiveInlining)] get => _access->IsCreated; }
            public int Count { [MethodImpl(AggressiveInlining)] get => _access->Count; }
            public int Capacity { [MethodImpl(AggressiveInlining)] get => _access->Capacity; }

            [MethodImpl(AggressiveInlining)]
            public bool TryGetValue(in TKey key, out TValue item) => _access->TryGetValue(in key, out item);

            [MethodImpl(AggressiveInlining)]
            public bool ContainsKey(in TKey key) => _access->Contains(in key);

            public ref readonly TValue this[in TKey key]
            {
                [MethodImpl(AggressiveInlining)] get
                {
                    ref var result = ref _access->TryGetRefValue<TValue>(in key);
                    if (Unsafe.IsNullRef(ref result))
                    {
                        ThrowKeyNotPresent(key);
                    }

                    return ref result;
                }
            }

            public Enumerator GetEnumerator() => new()
            {
                EnumeratorHelper = new HashMapHelperAccess<TKey>.Enumerator(_access)
            };
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void ThrowKeyNotPresent(in TKey key)
        {
            throw new ArgumentException($"Key: {key} is not present.");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private static void ThrowKeyAlreadyAdded(in TKey key)
        {
            throw new ArgumentException($"An item with the same key has already been added: {key}");
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS"), Conditional("UNITY_DOTS_DEBUG")]
        private readonly void ThrowAtMaxCapacity()
        {
            throw new InvalidOperationException($"Capacity is insufficient, and resize would fail (Capacity {Capacity} / {MaxCapacity}, Count {Count})!");
        }
    }
}
