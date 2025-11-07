using System.Diagnostics;
using SerializableValueObjects;
using SerializableValueObjects.Attributes;
using UnityEngine;

namespace Samples
{
    internal sealed class BehaviourWithDecimal : MonoBehaviour
    {
        [Header("Serializable Decimal")]
        [SerializeField] private SerializableDecimal _value = 42.24m;
        [DecimalFormat(DecimalFormatType.Integers)]
        [SerializeField] private SerializableDecimal _integerOnlyValue = 42;
        [DecimalRange(min: -42, max: 24)]
        [SerializeField] private SerializableDecimal _valueWithSlider = 42.24m;
        [DecimalFormat(DecimalFormatType.Integers)]
        [DecimalRange(min: -10, max: 10)]
        [SerializeField] private SerializableDecimal _integerValueWithSlider = 42;
        [DecimalFormat("N3")]
        [SerializeField] private SerializableDecimal _valueWithCustomFormat = 1222333.444m;
        [SerializeField] private SerializableDecimal _maxDecimalValue = decimal.MaxValue;
        [Header("Default Unity Numbers")]
        [SerializeField] private double _doubleValue = double.MaxValue;
        [SerializeField] private long _maxLongValue = long.MaxValue;

        [Conditional("UNITY_EDITOR")]
        private void Reset()
        {
            _ = _value;
            _ = _integerOnlyValue;
            _ = _valueWithSlider;
            _ = _integerValueWithSlider;
            _ = _valueWithCustomFormat;
            _ = _doubleValue;
            _ = _maxLongValue;
        }
    }
}
