using System;
using System.Collections.Generic;
using InspectorAttributes;
using Samples.References;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Generic.Samples
{
    internal sealed class BehaviourWithLoopsInInspectableReferenceAttribute : MonoBehaviour
    {
        [InspectableReference]
        [SerializeReference] private IDataWithLoop _start = default!;

        [InspectableReference]
        [SerializeReference] private List<IDataWithLoop> _nodes = default!;

        private void OnEnable()
        {
            _ = _nodes;
        }
    }

    [Serializable]
    internal sealed class SimpleNode : IDataWithLoop
    {
        string IDataWithLoop.Value => "Simple Node";
    }

    [Serializable]
    internal sealed class AdvancedNode : IDataWithLoop
    {
        [InspectableReference]
        [SerializeReference] private IDataWithLoop _another = default!;

        string IDataWithLoop.Value => _another.Value;
    }
}
