using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using static System.Runtime.CompilerServices.MethodImplOptions;

namespace NativeRefCollections.Runtime
{
    using CollectionsBridge;

    [NativeContainer]
    public unsafe struct NativeRefHashSet<T> : INativeDisposable
        where T : unmanaged, IEquatable<T>
    {
        private HashMapHelperAccess<T> _helper;

        public NativeRefHashSet(int initialCapacity, AllocatorManager.AllocatorHandle allocator)
        {
            _helper = default;
            _helper.Init(initialCapacity, allocator);
        }

        [MethodImpl(AggressiveInlining)]
        public bool Add(T item) =>_helper.Add(item);

        [MethodImpl(AggressiveInlining)]
        public bool TryAdd(T item) => -1 != _helper.TryAdd(item);

        [MethodImpl(AggressiveInlining)]
        public bool Remove(T item) => _helper.Remove(item);

        [MethodImpl(AggressiveInlining)]
        public readonly bool Contains(T item) => -1 != _helper.Find(item);

        [MethodImpl(AggressiveInlining)]
        public void Clear() => _helper.Clear();

        [MethodImpl(AggressiveInlining)]
        public void Dispose() => _helper.Dispose();

        [MethodImpl(AggressiveInlining)]
        public JobHandle Dispose(JobHandle inputDeps) => _helper.Dispose(inputDeps);

        [MethodImpl(AggressiveInlining)]
        public Enumerator GetEnumerator()
        {
            fixed (HashMapHelperAccess<T>* access = &_helper)
            {
                return new Enumerator { EnumeratorHelper = new HashMapHelperAccess<T>.Enumerator(access) };
            }
        }

        [MethodImpl(AggressiveInlining)]
        public ReadOnly AsReadOnly()
        {
            fixed (HashMapHelperAccess<T>* access = &_helper)
            {
                return new ReadOnly(access);
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        public ref struct Enumerator
        {
            internal HashMapHelperAccess<T>.Enumerator EnumeratorHelper;

            [MethodImpl(AggressiveInlining)]
            public bool MoveNext() => EnumeratorHelper.MoveNext();

            [MethodImpl(AggressiveInlining)]
            public void Reset() => EnumeratorHelper.Reset();

            public readonly ref T Current
            {
                [MethodImpl(AggressiveInlining)] get => ref EnumeratorHelper.GetCurrentKey();
            }
        }

        [NativeContainer]
        [NativeContainerIsReadOnly]
        public readonly struct ReadOnly
        {
            private readonly HashMapHelperAccess<T>* _access;

            internal ReadOnly(HashMapHelperAccess<T>* access)
            {
                _access = access;
            }

            public bool Contains(in T item) => _access->Contains(item);

            public ReadOnlyEnumerator GetEnumerator()
            {
                return new ReadOnlyEnumerator
                {
                    EnumeratorHelper = new HashMapHelperAccess<T>.Enumerator(_access)
                };
            }

            [NativeContainer]
            [NativeContainerIsReadOnly]
            public ref struct ReadOnlyEnumerator
            {
                internal HashMapHelperAccess<T>.Enumerator EnumeratorHelper;

                [MethodImpl(AggressiveInlining)]
                public bool MoveNext() => EnumeratorHelper.MoveNext();

                [MethodImpl(AggressiveInlining)]
                public void Reset() => EnumeratorHelper.Reset();

                public readonly ref readonly T Current
                {
                    [MethodImpl(AggressiveInlining)] get => ref EnumeratorHelper.GetCurrentKey();
                }
            }
        }
    }
}
