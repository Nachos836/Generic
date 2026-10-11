#nullable enable

using System;
using System.Diagnostics;
using UnityEngine;

using Debug = UnityEngine.Debug;

namespace Pooling
{
    /// <summary>
    /// Secondary root for gameplay-sensitive logic of a pooled object. <br/>
    /// Its subtree is <b>excluded</b> from activation warmup: keep heavy "mute" parts (renderers, colliders) outside of it. <br/>
    /// <see cref="StashedColliders"/> lists colliders <b>outside</b> of this subtree that must be warmed up
    /// on the pool's stash layer (no contacts, no trigger callbacks) and get their original layer back afterwards.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SensitiveLogicRoot : MonoBehaviour
    {
        [Tooltip("Colliders outside of this subtree that could trigger gameplay logic during warmup")]
        [SerializeField] private Collider[] _stashedColliders = Array.Empty<Collider>();

        internal ReadOnlySpan<Collider> StashedColliders => _stashedColliders;

        [Conditional("UNITY_EDITOR")]
        private void OnValidate()
        {
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
