using System;
using SerializableValueObjects;
using UnityEngine;

namespace Generic.Samples.GameplayInventory.Concrete
{
    public readonly struct Money : IItem
    {
        public decimal Amount { get; init; }
    }

    [Serializable]
    internal sealed class PickableMoney : IPickableItem<Money>
    {
        [SerializeField] private SerializableDecimal _nominalPrice = 1.0m;

        void IPickableItem<Money>.Accept(Inventory inventory, Action<Inventory, Money> callback)
        {
            callback(inventory, new Money
            {
                Amount = _nominalPrice
            });
        }
    }
}
