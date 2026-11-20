using System;
using Unity.Collections;
using UnityEngine;

namespace Generic.Samples.GameplayInventory.Concrete
{
    public readonly struct Prize : IItem
    {
        public int Place { get; init; }
        public FixedString32Bytes Reason { get; init; }
    }

    [Serializable]
    internal sealed class PickablePrize : IPickableItem<Prize>
    {
        [SerializeField] private int _place = 1;
        [SerializeField] private string _reason = "You are the best!";

        void IPickableItem<Prize>.Accept(Inventory inventory, Action<Inventory, Prize> callback)
        {
            callback(inventory, new Prize
            {
                Place = _place,
                Reason = _reason
            });
        }
    }
}
