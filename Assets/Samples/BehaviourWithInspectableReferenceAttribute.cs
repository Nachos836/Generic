using System;
using InspectorAttributes;
using Samples.References;
using UnityEngine;

namespace Generic.Samples
{
    internal sealed class BehaviourWithInspectableReferenceAttribute : MonoBehaviour
    {
        [InspectableReference]
        [SerializeReference] private IData _reference = default!;

        [SerializeField] private MyEnum _enum = MyEnum.A;

        private void OnEnable()
        {
            if (_reference is not null)
            {
                UnityEngine.Debug.LogFormat("[InspectableReferenceAttribute] Test: data contains: {0}", _reference.Value);
            }

            _ = _enum;
        }

        private enum MyEnum
        {
            A, B, C
        }
    }

    [Serializable]
    internal sealed class SecondDataOption : IData
    {
        string IData.Value => "Second option";
    }
}
