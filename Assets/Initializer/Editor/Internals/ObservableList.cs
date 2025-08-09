#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using JetBrains.Annotations;

namespace Initializer.Editor.Internals
{
    internal sealed class ObservableList<T> : IList<T>, IList, IDisposable
    {
        private readonly List<T> _inner;

        public ObservableList(int amount = 16) => _inner = new List<T>(amount);
        public ObservableList(IEnumerable<T> items)
        {
            _inner = new List<T>(items);
            foreach (var it in _inner) AttachItem(it);
        }

        private event Action<int, T>? ItemAdded;
        [MustUseReturnValue] public IDisposable ItemAddedSubscribe(Action<int, T> action)
        {
            ItemAdded += action;

            return new Subscription(() => ItemAdded -= action);
        }

        private event Action<int, T>? ItemRemoved;
        [MustUseReturnValue] public IDisposable ItemRemovedSubscribe(Action<int, T> action)
        {
            ItemRemoved += action;

            return new Subscription(() => ItemRemoved -= action);
        }

        private event Action<int, T, T>? ItemReplaced;
        [MustUseReturnValue] public IDisposable ItemReplacedSubscribe(Action<int, T, T> action)
        {
            ItemReplaced += action;

            return new Subscription(() => ItemReplaced -= action);
        }

        private event Action<int>? CountChanged;
        [MustUseReturnValue] public IDisposable CountChangedSubscribe(Action<int> action)
        {
            CountChanged += action;

            return new Subscription(() => CountChanged -= action);
        }

        private event Action? Cleared;
        private event Action<int, string>? ItemPropertyChanged;

        public T this[int index]
        {
            get => _inner[index];
            set
            {
                var old = _inner[index];
                if (EqualityComparer<T>.Default.Equals(old, value)) return;
                DetachItem(old);
                _inner[index] = value;
                AttachItem(value);
                ItemReplaced?.Invoke(index, old, value);
            }
        }

        public int Count => _inner.Count;

        bool ICollection.IsSynchronized => ((ICollection)_inner).IsSynchronized;
        object ICollection.SyncRoot => ((ICollection)_inner).SyncRoot;
        bool IList.IsFixedSize => ((IList)_inner).IsFixedSize;
        public bool IsReadOnly => ((ICollection<T>)_inner).IsReadOnly;

        object IList.this[int index]
        {
            get => ((IList)_inner)[index];
            set => ((IList)_inner)[index] = value;
        }

        public void Add(T item)
        {
            var idx = _inner.Count;
            _inner.Add(item);
            AttachItem(item);
            ItemAdded?.Invoke(idx, item);
            CountChanged?.Invoke(Count);
        }

        int IList.Add(object? value)
        {
            if (value is null) throw new ArgumentNullException(nameof(value));

            var item = (T) value;
            var index = ((IList)_inner).Add(value);
            AttachItem(item);
            ItemAdded?.Invoke(index, item);
            CountChanged?.Invoke(Count);
            return index;
        }

        public void Insert(int index, T item)
        {
            _inner.Insert(index, item);
            AttachItem(item);
            ItemAdded?.Invoke(index, item);
        }

        void IList.Insert(int index, object value) => Insert(index, (T) value);

        public bool Remove(T item)
        {
            var idx = _inner.IndexOf(item);
            if (idx < 0) return false;
            RemoveAt(idx);
            CountChanged?.Invoke(Count);
            return true;
        }

        public void RemoveAt(int index)
        {
            var old = _inner[index];
            DetachItem(old);
            _inner.RemoveAt(index);
            ItemRemoved?.Invoke(index, old);
            CountChanged?.Invoke(Count);
        }

        void IList.Remove(object value) => Remove((T) value);

        public void Clear()
        {
            foreach (var it in _inner) DetachItem(it);
            _inner.Clear();
            Cleared?.Invoke();
            CountChanged?.Invoke(Count);
        }

        public bool Contains(T item) => _inner.Contains(item);
        bool IList.Contains(object? value) => ((IList)_inner).Contains(value);

        public int IndexOf(T item) => _inner.IndexOf(item);
        int IList.IndexOf(object value) => ((IList)_inner).IndexOf(value);

        public void CopyTo(T[] array, int arrayIndex) => _inner.CopyTo(array, arrayIndex);
        void ICollection.CopyTo(Array array, int index) => CopyTo((T[]) array, index);

        public IEnumerator<T> GetEnumerator() => _inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void AttachItem(T item)
        {
            if (item is INotifyPropertyChanged candidate)
            {
                candidate.PropertyChanged += OnItemPropertyChanged;
            }
        }

        private void DetachItem(T item)
        {
            if (item is INotifyPropertyChanged candidate)
            {
                candidate.PropertyChanged -= OnItemPropertyChanged;
            }
        }

        private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs args)
        {
            if (sender is not T item) return;

            var index = _inner.IndexOf(item);
            if (index >= 0)
            {
                ItemPropertyChanged?.Invoke(index, args.PropertyName ?? string.Empty);
            }
        }

        public void Dispose()
        {
            foreach (var it in _inner)
            {
                DetachItem(it);
            }

            _inner.Clear();
            Cleared?.Invoke();
            CountChanged?.Invoke(Count);

            Cleared = null;
            CountChanged = null;
            ItemAdded = null;
            ItemRemoved = null;
            ItemReplaced = null;
            ItemPropertyChanged = null;
        }
    }
}
