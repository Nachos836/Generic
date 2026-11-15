using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static System.Runtime.CompilerServices.MethodImplOptions;

namespace NativeRefCollections.Runtime.CollectionsBridge
{
    internal unsafe struct HashMapHelperAccess<T> : INativeDisposable
        where T : unmanaged, IEquatable<T>
    {
        private HashMapHelper<T> _helper;

        public const int MaxCapacity = HashMapHelper<T>.kMaxCapacity;

        private readonly int AllocatedIndex { [MethodImpl(AggressiveInlining)] get => _helper.AllocatedIndex; }
        private readonly int* Buckets { [MethodImpl(AggressiveInlining)] get => _helper.Buckets;  }
        private readonly int* Next { [MethodImpl(AggressiveInlining)] get => _helper.Next; }
        private readonly T* Keys { [MethodImpl(AggressiveInlining)] get => _helper.Keys; }
        public readonly bool IsCreated { [MethodImpl(AggressiveInlining)] get => _helper.IsCreated;}
        public readonly bool IsEmpty { [MethodImpl(AggressiveInlining)] get => _helper.IsEmpty; }
        public readonly int Count { [MethodImpl(AggressiveInlining)]  get => _helper.Count; }
        public readonly byte* Ptr { [MethodImpl(AggressiveInlining)] get => _helper.Ptr; }

        public int Capacity
        {
            [MethodImpl(AggressiveInlining)] readonly get => _helper.Capacity;
            [MethodImpl(AggressiveInlining)] set => _helper.Capacity = value;
        }

        public void Init(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            _helper.Init(initialCapacity, sizeOfValueT: Unsafe.SizeOf<T>(), HashMapHelper<T>.kMinCapacity, allocator);
        }

        public static HashMapHelperAccess<T>* Alloc(int capacity, AllocatorManager.AllocatorHandle allocator)
        {
            var data = (HashMapHelperAccess<T>*)Memory.Unmanaged.Allocate(sizeof(HashMapHelper<T>), UnsafeUtility.AlignOf<HashMapHelper<T>>(), allocator);
            data->Init(capacity, allocator);

            return data;
        }

        public static void Free(HashMapHelper<T>* instance)
        {
            if (instance == null) throw new InvalidOperationException("Hash based container has yet to be created or has been destroyed!");

            instance->Dispose();

            Memory.Unmanaged.Free(instance, instance->Allocator);
        }

        [MethodImpl(AggressiveInlining)]
        public bool Add(in T item) => -1 != _helper.TryAdd(item);

        [MethodImpl(AggressiveInlining)]
        public int TryAdd(in T item) => _helper.TryAdd(item);

        [MethodImpl(AggressiveInlining)]
        public bool Remove(in T item) => -1 != _helper.TryRemove(item);

        [MethodImpl(AggressiveInlining)]
        public int TryRemove(in T item) => _helper.TryRemove(item);

        [MethodImpl(AggressiveInlining)]
        public readonly bool TryGetValue<TValue>(in T key, out TValue item) where TValue : unmanaged
            => TryGetValue(key, out item);

        [MethodImpl(AggressiveInlining)]
        public readonly bool Contains(in T item) => -1 != Find(item);

        [MethodImpl(AggressiveInlining)]
        public void Clear() => _helper.Clear();

        [MethodImpl(AggressiveInlining)]
        public void TrimExcess() => _helper.TrimExcess();

        [MethodImpl(AggressiveInlining)]
        public void Dispose()
        {
            if (_helper.IsCreated is false) return;

            _helper.Dispose();
        }

        [MethodImpl(AggressiveInlining)]
        public JobHandle Dispose(JobHandle inputDeps)
        {
            if (_helper.IsCreated is false) return inputDeps;

            var jobHandle = new UnsafeDisposeJob { Ptr = _helper.Ptr, Allocator = _helper.Allocator }
                .Schedule(inputDeps);
            _helper.Ptr = null;

            return jobHandle;
        }

        [MethodImpl(AggressiveInlining)]
        public readonly int Find(in T key)
        {
            if (AllocatedIndex <= 0) return -1;

            var bucket = GetBucket(key);
            var entryIdx = Buckets[bucket];

            if ((uint)entryIdx >= (uint)Capacity) return -1;

            var nextPointers = Next;
            while (!UnsafeUtility.ReadArrayElement<T>(Keys, entryIdx).Equals(key))
            {
                entryIdx = nextPointers[entryIdx];
                if ((uint)entryIdx >= (uint)Capacity) return -1;
            }

            return entryIdx;
        }

        [MethodImpl(AggressiveInlining)]
        public readonly bool TryGetValue<TValue>(T key, out TValue item) where TValue : unmanaged
        {
            var idx = Find(key);

            if (-1 != idx)
            {
                item = UnsafeUtility.ReadArrayElement<TValue>(Ptr, idx);
                return true;
            }

            item = default;
            return false;
        }

        [MethodImpl(AggressiveInlining)]
        public readonly ref TValue TryGetRefValue<TValue>(in T key) where TValue : unmanaged
        {
            var idx = Find(in key);

            if (-1 != idx)
            {
                return ref UnsafeUtility.ArrayElementAsRef<TValue>(Ptr, idx);
            }

            return ref Unsafe.NullRef<TValue>();
        }

        [MethodImpl(AggressiveInlining)]
        private readonly int GetBucket(in T key)
        {
            return (int)((uint)key.GetHashCode() & (_helper.BucketCapacity - 1));
        }

        public struct Enumerator
        {
            [NativeDisableUnsafePtrRestriction] private readonly HashMapHelper<T>* _data;

            private int _index;
            private int _bucketIndex;
            private int _nextIndex;

            public Enumerator(HashMapHelperAccess<T>* access)
            {
                _data = &access->_helper;
                _index = -1;
                _bucketIndex = 0;
                _nextIndex = -1;
            }

            [MethodImpl(AggressiveInlining)]
            public bool MoveNext()
            {
                return _data->MoveNext(ref _bucketIndex, ref _nextIndex, out _index);
            }

            public void Reset()
            {
                _index = -1;
                _bucketIndex = 0;
                _nextIndex = -1;
            }

            [MethodImpl(AggressiveInlining)]
            public readonly ref T GetCurrentKey() => ref _index != -1
                ? ref _data->Keys[_index]
                : ref Unsafe.NullRef<T>();

            [MethodImpl(AggressiveInlining)]
            public readonly KVPair<T, TValue> GetCurrent<TValue>() where TValue : unmanaged
                => new() { m_Data = _data, m_Index = _index };
        }
    }
}
