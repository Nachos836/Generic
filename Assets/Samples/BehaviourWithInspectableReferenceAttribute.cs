using System;
using InspectorAttributes;
using JetBrains.Annotations;
using Samples.References;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace Generic.Samples
{
    internal sealed class BehaviourWithInspectableReferenceAttribute : MonoBehaviour
    {
        [Header("Custom menu for SerializeReference")]
        [InspectableReference]
        [SerializeReference] private IData _reference = default!;
        [InspectableReference]
        [SerializeReference] private IData[] _references = default!;

        [Header("Default Unity's Enum as example")]
        [SerializeField] private MyEnum _enum = MyEnum.A;

        private void OnEnable()
        {
            if (_reference is not null)
            {
                UnityEngine.Debug.LogFormat("[InspectableReferenceAttribute] Test: data contains: {0}", _reference.Value);
            }

            _ = _enum;
            _ = _references;
        }

        private enum MyEnum
        {
            [UsedImplicitly] A,
            [UsedImplicitly] B,
            [UsedImplicitly] C
        }
    }

    [Serializable]
    internal sealed class SecondDataOption : IData
    {
        string IData.Value => "Second option";
    }

    [Serializable]
    internal sealed class NestedDataOption : IData
    {
        [InspectableReference]
        [SerializeReference] private IData _inner = default!;

        string IData.Value => _inner.Value;
    }
}
