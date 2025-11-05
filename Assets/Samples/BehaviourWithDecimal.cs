using SerializableValueObjects;
using SerializableValueObjects.Attributes;
using UnityEngine;

namespace Samples
{
    internal sealed class BehaviourWithDecimal : MonoBehaviour
    {
        [SerializeField] private SerializableDecimal _value = 42.24m;
        [DecimalFormat(DecimalFormatType.Integers)]
        [SerializeField] private SerializableDecimal _integerOnlyValue = 42;
        [DecimalRange(min: -42, max: 24)]
        [SerializeField] private SerializableDecimal _valueWithSlider = 42.24m;
        [DecimalFormat(DecimalFormatType.Integers)]
        [DecimalRange(min: -10, max: 10)]
        [SerializeField] private SerializableDecimal _integerValueWithSlider = 42;
        [SerializeField] private double _doubleValue = 42;

        private void OnEnable()
        {
            _ = _value;
            _ = _integerOnlyValue;
            _ = _valueWithSlider;
            _ = _integerValueWithSlider;
            _ = _doubleValue;
        }
    }
}
