#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using NativeRefCollections.Runtime;
using Unity.Burst;
using Unity.Collections;

// ReSharper disable once CheckNamespace
namespace Generic.Samples.GameplayInventory
{
    [BurstCompile]
    [CompilerGenerated]
    public sealed partial class Inventory : IDisposable
    {
        private NativeRefHashMap<Item.TypeId, Item> _map;

        /// <summary>
        /// Event that indicates that some Item is changed, or Items amount changed
        /// </summary>
        public event Action? ItemsChanged;

        public Inventory()
        {
            _map = new (initialCapacity: Item.TypeIdCount, Allocator.Domain);
        }

        public Inventory(IEnumerable<Item> items) : this()
        {
            foreach (var item in items)
            {
                var type = Item.GetType(in item);
                _map.Add(type, item);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeRefHashMap<Item.TypeId, Item>.ReadOnly ProvideItems()
        {
            return _map.AsReadOnly();
        }

        public void Add<TItem>(in TItem item) where TItem : unmanaged, IItem
        {
            var id = Item.GetType<TItem>();
            var union = item.ToUnion();
            _map.Add(id, union);

            ItemsChanged?.Invoke();
        }

        public void Remove<TItem>() where TItem : unmanaged, IItem
        {
            var type = Item.GetType<TItem>();
            if (_map.Remove(type) is false) throw new KeyNotFoundException(type.ToString());

            ItemsChanged?.Invoke();
        }

        [Pure]
        public ref readonly TItem Read<TItem>() where TItem : unmanaged, IItem
        {
            var type = Item.GetType<TItem>();
            ref readonly var candidate = ref _map[type];

            return ref candidate.Cast<TItem>();
        }

        public void Write<TItem>(in TItem item) where TItem : unmanaged, IItem
        {
            var type = Item.GetType<TItem>();
            var union = item.ToUnion();
            _map[type] = union;

            ItemsChanged?.Invoke();
        }

        public bool TryAdd<TItem>(in TItem item) where TItem : unmanaged, IItem
        {
            var type = Item.GetType<TItem>();
            var union = item.ToUnion();
            if (_map.TryAdd(type, union) is false) return false;

            ItemsChanged?.Invoke();
            return true;
        }

        public bool TryRemove<TItem>() where TItem : unmanaged, IItem
        {
            var type = Item.GetType<TItem>();
            if (_map.Remove(type) is false) return false;

            ItemsChanged?.Invoke();
            return true;
        }

        [Pure]
        public ref readonly TItem TryRead<TItem>() where TItem : unmanaged, IItem
        {
            var type = Item.GetType<TItem>();
            ref var item = ref _map[type];
            if (Unsafe.IsNullRef(ref item)) return ref Unsafe.NullRef<TItem>();

            return ref item.Cast<TItem>();
        }

        public bool TryWrite<TItem>(in TItem item) where TItem : unmanaged, IItem
        {
            var type = Item.GetType<TItem>();
            if (_map.ContainsKey(type) is false) return false;

            var union = item.ToUnion();
            _map[type] = union;

            ItemsChanged?.Invoke();
            return true;
        }

        public void AddOrSet<TItem>(in TItem item) where TItem : unmanaged, IItem
        {
            var type = Item.GetType<TItem>();
            var union = item.ToUnion();

            _map.SetValue(in type, in union);
        }

        public void Dispose()
        {
            _map.Dispose();
            _map = default!;
        }
    }
}
