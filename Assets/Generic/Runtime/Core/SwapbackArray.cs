#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Generic.Core
{
    [PublicAPI]
    [StructLayout(LayoutKind.Sequential)]
    public struct SwapbackArray<T>
    {
        private T[] _items;

        public int Count
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private set;
        }

        public int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _items.Length;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => ResizeIfNeeded(value);
        }

        public SwapbackArray(int capacity = 16)
        {
            _items = new T[capacity];
            Count = 0;
        }

        public SwapbackArray(ReadOnlySpan<T> items)
        {
            _items = new T[items.Length];
            Count = _items.Length;

            items.CopyTo(_items);
        }

        public SwapbackArray(ICollection<T> items)
        {
            _items = new T[items.Count];
            Count = _items.Length;

            items.CopyTo(_items, 0);
        }

        public SwapbackArray(T[] items)
        {
            _items = new T[items.Length];
            Count = _items.Length;
        }

        [MustDisposeResource]
        public RemoveHandler Add(T item)
        {
            ResizeIfNeeded(Count + 1);
            _items[Count] = item;

            return new RemoveHandler(this, Count++);
        }

        public void Clear()
        {
            Array.Clear(_items, 0, Count);
            Count = 0;
        }

        /// <summary>
        /// Sets the length of this list, increasing the capacity if necessary.
        /// </summary>
        /// <remarks>Does not clear newly allocated bytes.</remarks>
        /// <param name="length">The new length of this list.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ResizeUninitialized(int length) => ResizeIfNeeded(length);

        /// <summary>
        /// The element at a given index.
        /// </summary>
        /// <param name="index">An index into this list.</param>
        /// <value>The value to store at the `index`.</value>
        /// <exception cref="IndexOutOfRangeException">Thrown if `index` is out of bounds.</exception>
        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _items[index];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => _items[index] = value;
        }

        public IEnumerable<T> this[Range range]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _items[range];
        }

        public readonly Enumerator GetEnumerator() => new (_items.AsSpan());

        private void RemoveAt(int index)
        {
            var lastIndex = Count - 1;
            if (index != lastIndex)
            {
                _items[index] = _items[lastIndex];
            }

            _items[lastIndex] = default!;
            Count--;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ResizeIfNeeded(int newCapacity)
        {
            if (_items.Length >= newCapacity) return;

            newCapacity = Math.Max(Capacity * 2, newCapacity);
            Capacity = newCapacity;
            Array.Resize(ref _items, Capacity);
        }

        public struct RemoveHandler : IDisposable
        {
            private readonly int _index;
            private SwapbackArray<T> _collection;

            internal RemoveHandler(SwapbackArray<T> collection, int index)
            {
                _collection = collection;
                _index = index;
            }

            public void Dispose() => _collection.RemoveAt(_index);
        }

        public ref struct Enumerator
        {
            private readonly ReadOnlySpan<T> _readOnlyView;
            private int _index;

            internal Enumerator(ReadOnlySpan<T> readOnlyView)
            {
                _readOnlyView = readOnlyView;
                _index = -1;
            }

            [UsedImplicitly]
            public readonly ref readonly T Current => ref _readOnlyView[_index]!;

            [UsedImplicitly]
            public bool MoveNext()
            {
                ++_index;

                return _index < _readOnlyView.Length - 1;
            }
        }
    }
}
