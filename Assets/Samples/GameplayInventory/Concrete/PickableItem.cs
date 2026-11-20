using System;
using System.Diagnostics;
using Generic.Core;
using InspectorAttributes;
using UnityEngine;

namespace Generic.Samples.GameplayInventory.Concrete
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    internal sealed class PickableItem : MonoBehaviour, IPickableItem
    {
        [ReadOnly]
        [SerializeField] private GameObject _root = default!;
        [Required]
        [InspectableReference]
        [SerializeReference] private IPickableItem _pickableItem = default!;

        private IDisposable? _pickHandler;

        [Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            _root = gameObject;
        }

        [Conditional("UNITY_EDITOR")]
        private void Reset() => OnValidate();

        private void Awake()
        {
            _pickHandler ??= Disposable.CreateWithState(_root, Destroy);
        }

        private void OnDestroy()
        {
            _pickHandler?.Dispose();
            _pickHandler = null;
        }

        void IPickableItem.ProvideTo(Inventory inventory)
        {
            _pickableItem.ProvideTo(inventory);

            _pickHandler?.Dispose();
            _pickHandler = null;
        }
    }
}
