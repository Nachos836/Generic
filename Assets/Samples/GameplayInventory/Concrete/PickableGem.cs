using System;
using SerializableValueObjects;
using UnityEngine;

namespace Generic.Samples.GameplayInventory.Concrete
{
    public readonly struct Gem : IItem
    {
        public decimal Amount { get; init; }
    }

    [Serializable]
    internal sealed class PickableGem : IPickableItem<Gem>
    {
        [SerializeField] private SerializableDecimal _amount = 1.0m;

        public void Accept(Inventory inventory, Action<Inventory, Gem> callback)
        {
            callback(inventory, new Gem
            {
                Amount = _amount
            });
        }
    }
}
