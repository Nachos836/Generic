#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace SerializableValueObjects.Editor.Decimal
{
    /// <summary>
    /// Makes a text field for entering decimals
    /// </summary>
    [UxmlElement]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [SuppressMessage("ReSharper", "MemberCanBePrivate.Global")]
    public sealed partial class DecimalField : TextValueField<decimal>
    {
        private const string DefaultFormat = "G";
        private const NumberStyles DefaultNumberStyle = NumberStyles.Number;
        private static readonly IFormatProvider DefaultFormatProvider = CultureInfo.GetCultureInfo("en-US").NumberFormat;

        /// <summary>
        /// USS class name of elements for this type
        /// </summary>
        public new const string ussClassName = "custom-decimal-field";
        /// <summary>
        /// USS class name of labels in elements of this type
        /// </summary>
        public new const string labelUssClassName = ussClassName + "__label";
        /// <summary>
        /// <para>USS class name of input elements in elements of this type.</para>
        /// </summary>
        public new const string inputUssClassName = ussClassName + "__input";

        // Note: base constructor will trigger those, so we provide fallback
        // ReSharper disable once MemberInitializerValueIgnored
        private readonly Func<decimal, string?> _valueToStringRoutine = static income => income.ToString(DefaultFormat, DefaultFormatProvider);
        // ReSharper disable once MemberInitializerValueIgnored
        private readonly Func<string, decimal?> _stringToValueRoutine = static income => decimal.Parse(income, DefaultNumberStyle, DefaultFormatProvider);

        private DecimalInput GetDecimalInput => (DecimalInput) textInputBase;

        protected override string ValueToString(decimal income)
        {
            return _valueToStringRoutine.Invoke(income)
                ?? text;
        }

        protected override decimal StringToValue(string income)
        {
            return _stringToValueRoutine.Invoke(income)
                ?? value;
        }

        public DecimalField(): this
        (
            label: string.Empty,
            DefaultFormat,
            UINumericFieldsUtils.k_AllowedCharactersForFloat,
            DefaultNumberStyle
        ) { }

        public DecimalField(string label, string format, string allowedCharacters, NumberStyles numberStyles)
            : base(label, 256, new DecimalInput(format, allowedCharacters))
        {
            _valueToStringRoutine = income =>
            {
                return income.ToString(format: format, DefaultFormatProvider);
            };
            _stringToValueRoutine = income =>
            {
                return decimal.TryParse(income, numberStyles, DefaultFormatProvider, out var result)
                    ? result
                    : value;
            };

            AddToClassList(ussClassName);
            labelElement.AddToClassList(labelUssClassName);
            AddLabelDragger<decimal>();

            autoCorrection = true;
            formatString = format;
        }

        /// <summary>
        /// Applies the values of a 3D delta and a speed from an input device.
        /// </summary>
        /// <param name="delta">A vector used to compute the value change.</param>
        /// <param name="speed">A multiplier for the value change.</param>
        /// <param name="startValue">The start value.</param>
        public override void ApplyInputDeviceDelta(Vector3 delta, DeltaSpeed speed, decimal startValue)
        {
            GetDecimalInput.ApplyInputDeviceDelta(delta, speed, startValue);
        }

        private sealed class DecimalInput : TextValueInput
        {
            private DecimalField ParentField => (DecimalField) parent;

            protected override string allowedCharacters { get; }

            internal DecimalInput(string formatString, string allowedCharacters)
            {
                this.formatString = formatString;
                this.allowedCharacters = allowedCharacters;
            }

            public override void ApplyInputDeviceDelta(Vector3 delta, DeltaSpeed speed, decimal startValue)
            {
                try
                {
                    var dragSensitivity = CalculateDragSensitivity(startValue);
                    var acceleration = NumericFieldDraggerUtility.Acceleration
                    (
                        shiftPressed: speed == DeltaSpeed.Fast,
                        altPressed: speed == DeltaSpeed.Slow
                    );
                    var currentValue = StringToValue(ParentField.text);
                    var candidate = (decimal) Mathf.RoundBasedOnMinimumDifference
                    (
                        valueToRound: (double) (currentValue + (decimal) NumericFieldDraggerUtility.NiceDelta(delta, acceleration) * dragSensitivity),
                        minDifference: (double) dragSensitivity
                    );
                    if (ParentField.isDelayed)
                    {
                        ParentField.text = ValueToString(candidate);
                    }
                    else
                    {
                        ParentField.value = candidate;
                    }
                }
                catch (OverflowException exception)
                {
                    Debug.LogException(exception);
                }
            }

            protected override string ValueToString(decimal income) => ParentField.ValueToString(income);
            protected override decimal StringToValue(string income) => ParentField.StringToValue(income);

            private static decimal CalculateDragSensitivity(decimal value)
            {
                return Math.Max(1.0m, (decimal) Math.Pow(Math.Abs((double) value), 0.5)) * 0.029999999329447746m;
            }
        }
    }
}
