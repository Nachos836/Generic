#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Initializer.Editor.Internals
{
    internal sealed class ObservableList<T> : IDisposable
    {
        private readonly List<T> _items;

        public IList<T> RawList => _items;
        public IList ObjectRawList => _items;

        public static ObservableList<T> WrapList(List<T> list) => new (list);

        public ObservableList(int amount = 16)
        {
            _items = new List<T>(amount);
        }

        public ObservableList(IEnumerable<T> items)
        {
            _items = new List<T>(items);
        }

        private ObservableList(List<T> items)
        {
            _items = items;
        }

        private event Action<IReadOnlyCollection<T>>? ItemsAdded;

        [MustUseReturnValue]
        public IDisposable ItemsAddedSubscribe(Action<IReadOnlyCollection<T>> action)
        {
            ItemsAdded += action;

            return new Subscription(() => ItemsAdded -= action);
        }

        private event Action<IReadOnlyCollection<T>>? ItemsRemoved;

        [MustUseReturnValue]
        public IDisposable ItemsRemovedSubscribe(Action<IReadOnlyCollection<T>> action)
        {
            ItemsRemoved += action;

            return new Subscription(() => ItemsRemoved -= action);
        }

        private event Action<int>? CountChanged;

        [MustUseReturnValue]
        public IDisposable CountChangedSubscribe(Action<int> action)
        {
            CountChanged += action;

            return new Subscription(() => CountChanged -= action);
        }

        public T this[int index] => _items[index];

        public int Count => _items.Count;

        public void Remove(IReadOnlyCollection<T> items)
        {
            foreach (var item in items)
            {
                _items.Remove(item);
            }

            ItemsRemoved?.Invoke(items);
            CountChanged?.Invoke(Count);
        }

        public void Remove(T item)
        {
            _items.Remove(item);

            CountChanged?.Invoke(Count);
        }

        public void Add(IReadOnlyCollection<T> items)
        {
            foreach (var item in items)
            {
                _items.Add(item);
            }

            ItemsAdded?.Invoke(items);
            CountChanged?.Invoke(Count);
        }

        public void Add(T item)
        {
            _items.Add(item);

            CountChanged?.Invoke(Count);
        }

        public void Clear() => _items.Clear();

        public void Dispose()
        {
            ItemsAdded = null;
            ItemsRemoved = null;
            CountChanged = null;
        }
    }
}
