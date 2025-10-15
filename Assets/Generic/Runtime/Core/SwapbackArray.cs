#nullable enable

using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Generic.Core
{
    [Obsolete("WIP: Use on your own risk!")]
    public struct SwapbackArray<T>
    {
        private T[] _items;

        public int Count { get; private set; }

        public SwapbackArray(int capacity = 16)
        {
            _items = new T[capacity];
            Count = 0;
        }

        public SwapbackArray(ReadOnlySpan<T> items)
        {
            _items = new T[items.Length];
            Count = items.Length;

            items.CopyTo(_items);
        }

        public SwapbackArray(ICollection<T> items)
        {
            _items = new T[items.Count];
            Count = items.Count;

            items.CopyTo(_items, 0);
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

        private void ResizeIfNeeded(int capacity)
        {
            if (_items.Length >= capacity) return;

            var newCapacity = Math.Max(_items.Length * 2, capacity);
            Array.Resize(ref _items, newCapacity);
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

            public readonly ref readonly T Current => ref _readOnlyView[_index]!;

            public bool MoveNext()
            {
                ++_index;

                return _index < _readOnlyView.Length - 1;
            }
        }
    }
}
