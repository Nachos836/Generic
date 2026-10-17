#nullable enable

using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace Pooling
{
    /// <summary>
    /// <b>Authoring-only</b> marker: read once on the pool's immutable prototype copy and stripped before any clone is made. <br/>
    /// Its subtree is <b>excluded</b> from activation warmup: keep heavy "mute" parts (renderers, colliders) outside of it. <br/>
    /// <see cref="StashedColliders"/> lists colliders <b>outside</b> of this subtree that must be warmed up
    /// on <see cref="StashLayer"/> (no contacts, no trigger callbacks) and get their original layer back afterwards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SensitiveLogicRoot : MonoBehaviour
    {
        [Tooltip("Colliders outside of this subtree that could trigger gameplay logic during warmup")]
        [SerializeField] private Collider[] _stashedColliders = Array.Empty<Collider>();
        [SerializeField] private LayerMask _stashLayer = Physics.IgnoreRaycastLayer;

        /// <summary> Layer <b>index</b> (not mask). <see cref="OnValidate"/> guarantees exactly one bit. </summary>
        internal int StashLayer => math.tzcnt(_stashLayer.value);
        internal ReadOnlySpan<Collider> StashedColliders => _stashedColliders;

        [Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
            var rawLayer = new BitField32((uint) _stashLayer.value);
            if (rawLayer.CountBits() is 0 or > 1) throw new ArgumentOutOfRangeException(nameof(_stashLayer), _stashLayer, "More than one layer or no-layer is not supported");

            var root = transform.root;
            foreach (var collider in _stashedColliders)
            {
                if (collider == null)
                {
                    Debug.LogWarning($"[{nameof(SensitiveLogicRoot)}] '{name}' has an empty stashed collider slot.", this);
                    continue;
                }

                var target = collider.transform;
                if (target.IsChildOf(root) is false)
                {
                    Debug.LogWarning($"[{nameof(SensitiveLogicRoot)}] '{collider.name}' is outside of the pooled hierarchy.", this);
                    continue;
                }

                for (var current = target; current != null; current = current.parent)
                {
                    if (current.TryGetComponent<SensitiveLogicRoot>(out _) is false) continue;

                    Debug.LogWarning($"[{nameof(SensitiveLogicRoot)}] '{collider.name}' is inside a sensitive subtree: " +
                                     "it is not activated during warmup, stashing it is pointless.", this);
                    break;
                }
            }
        }
    }
}
