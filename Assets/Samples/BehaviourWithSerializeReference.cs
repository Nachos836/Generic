using System;
using UnityEngine;

namespace Generic.Samples
{
    internal sealed class BehaviourWithSerializeReference : MonoBehaviour
    {
        [SerializeField] private int _value = default!;
        [SerializeReference] private int _value2 = default!;
        [SerializeReference] private IMyType _reference1;
        [SerializeReference] private IMyType _reference2;

        private void OnValidate()
        {
            _reference1 = new MyType();
            _reference2 = new MyTypeWithData();

            _ = _value;
            _ = _value2;
            _ = _reference1;
            _ = _reference2;
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

        public MyTypeWithData()
        {
            _ = _data;
        }
    }
}
