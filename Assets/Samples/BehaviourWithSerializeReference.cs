using System;
using UnityEngine;

namespace Generic.Samples
{
    internal sealed class BehaviourWithSerializeReference : MonoBehaviour
    {
        [SerializeField] private int _value;
        [SerializeReference] private int _value2;
        [SerializeReference] private IMyType _reference1;
        [SerializeReference] private IMyType _reference2;

        private void OnValidate()
        {
            _reference1 = new MyType();
            _reference2 = new MyTypeWithData();
        }
    }

    internal interface IMyType
    {
    }

    [Serializable]
    internal sealed class MyType : IMyType
    {

    }

    [Serializable]
    internal sealed class MyTypeWithData : IMyType
    {
        [SerializeField] private string _data = "Cyka!";
    }
}
