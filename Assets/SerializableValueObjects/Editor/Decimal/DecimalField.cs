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
        private static readonly IFormatProvider DefaultFormat = CultureInfo.InvariantCulture.NumberFormat;

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

        private DecimalInput GetDecimalInput => (DecimalInput) textInputBase;

        protected override string ValueToString(decimal income)
        {
            return income.ToString(formatString, DefaultFormat);
        }

        protected override decimal StringToValue(string income)
        {
            return decimal.TryParse(income, NumberStyles.Float, DefaultFormat, out var result)
                ? result
                : value;
        }

        public DecimalField() : this(label: string.Empty, format: "") { }

        /// <param name="maxLength">Maximum number of characters the field can take.</param>
        /// <param name="label"></param>
        /// <param name="format"></param>
        /// <param name="integersOnly"></param>
        public DecimalField(string label, int maxLength = 1000, string format = "", bool integersOnly = false)
            : base(label, maxLength, new DecimalInput(format, integersOnly))
        {
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

            internal DecimalInput(string format, bool integersOnly)
            {
                formatString = format;
                allowedCharacters = integersOnly
                    ? "0123456789"
                    : UINumericFieldsUtils.k_AllowedCharactersForFloat;
            }

            protected override string allowedCharacters { get; }

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
                    var currentValue = StringToValue(text);
                    var candidate = (decimal)Mathf.RoundBasedOnMinimumDifference
                    (
                        valueToRound: (double) (currentValue + (decimal) NumericFieldDraggerUtility.NiceDelta(delta, acceleration) * dragSensitivity),
                        minDifference: (double) dragSensitivity
                    );
                    if (ParentField.isDelayed)
                    {
                        text = ValueToString(candidate);
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

            protected override string ValueToString(decimal income) => income.ToString(formatString);
            protected override decimal StringToValue(string income) => ParentField.StringToValue(income);

            private static decimal CalculateDragSensitivity(decimal value)
            {
                return Math.Max(1.0m, (decimal) Math.Pow(Math.Abs((double) value), 0.5)) * 0.029999999329447746m;
            }
        }
    }
}
