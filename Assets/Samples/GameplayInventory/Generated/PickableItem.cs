#nullable enable

using System;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Generic.Samples.GameplayInventory
{
    [CompilerGenerated]
    internal interface IPickableItem
    {
        void ProvideTo(Inventory _);
    }

    [CompilerGenerated]
    internal interface IPickableItem<out TItem> : IPickableItem where TItem : unmanaged, IItem
    {
        void Accept(Inventory inventory, Action<Inventory, TItem> callback);

        void IPickableItem.ProvideTo(Inventory inventory)
        {
            Accept(inventory, static (inventory, income) =>
            {
                inventory.AddOrSet(in income);
            });
        }
    }
}
