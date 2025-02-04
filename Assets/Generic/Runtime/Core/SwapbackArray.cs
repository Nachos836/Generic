#nullable enable

using System;
using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Generic.Core
{
    public class SwapbackArray<T>
    {
        private ImmutableArray<T?> _readOnlyView;
        private T?[] _items;

        public int Count { get; private set; }

        public SwapbackArray(int capacity = 16)
        {
            _items = new T[capacity];
            _readOnlyView = ImmutableCollectionsMarshal.AsImmutableArray(_items);
            Count = 0;
        }

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

        private void RemoveAt(int index)
        {
            var lastIndex = Count - 1;
            if (index != lastIndex)
            {
                _items[index] = _items[lastIndex];
            }

            _items[lastIndex] = default;
            Count--;
        }

        private void ResizeIfNeeded(int capacity)
        {
            if (_items.Length >= capacity) return;

            var newCapacity = Math.Max(_items.Length * 2, capacity);
            Array.Resize(ref _items, newCapacity);
            _readOnlyView = ImmutableCollectionsMarshal.AsImmutableArray(_items);
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(_readOnlyView.AsSpan());
        }

        public readonly struct RemoveHandler : IDisposable
        {
            private readonly SwapbackArray<T> _collection;
            private readonly int _index;

            internal RemoveHandler(SwapbackArray<T> collection, int index)
            {
                _collection = collection ?? throw new ArgumentNullException(nameof(collection));
                _index = index;
            }

            public void Dispose()
            {
                _collection.RemoveAt(_index);
            }
        }

        public ref struct Enumerator
        {
            private readonly ReadOnlySpan<T?> _readOnlyView;
            private int _index;

            internal Enumerator(ReadOnlySpan<T?> readOnlyView)
            {
                _readOnlyView = readOnlyView;
                _index = 0;
            }

            public ref readonly T Current => ref _readOnlyView[_index]!;

            public bool MoveNext()
            {
                while (_index < _readOnlyView.Length - 1)
                {
                    ++_index;
                    if (_readOnlyView[_index] != null) return true;
                }

                return false;
            }
        }
    }
}
