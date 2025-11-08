#nullable enable

using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SerializableValueObjects.Editor.Decimal
{
    using Attributes;

    using static Attributes.DecimalFormatType;

    [CustomPropertyDrawer(typeof(SerializableDecimal))]
    internal sealed class SerializableDecimalDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var attributes = fieldInfo.GetCustomAttributes(typeof(Attribute), inherit: true);
            var formatAttribute = attributes.Matches(DecimalFormatAttribute.None);
            var rangeAttribute = attributes.Matches(DecimalRangeAttribute.None);
            var integersOnly = formatAttribute.FormatType.Contains(flag: Integers);
            var positiveOnly = formatAttribute.FormatType.Contains(flag: PositiveOnly);
            var customFormat = formatAttribute.CustomFormat;
            var numberStyles = integersOnly ? NumberStyles.Integer : NumberStyles.Number;
            var allowedCharacters = (integersOnly, positiveOnly) switch
            {
                (integersOnly: true, positiveOnly: true) => "0123456789",
                (integersOnly: true, positiveOnly: false) => "0123456789-",
                (integersOnly: false, positiveOnly: false) => UINumericFieldsUtils.k_AllowedCharactersForFloat,
                (integersOnly: false, positiveOnly: true) => UINumericFieldsUtils.k_AllowedCharactersForFloat
                    .Except("-")
                    .ToString()
            };

            return rangeAttribute.IsNeeded
                ? CreateSliderField(property, rangeAttribute, customFormat, allowedCharacters, numberStyles, integersOnly)
                : CreateRegularField(property, customFormat, allowedCharacters, numberStyles);
        }

        private static VisualElement CreateRegularField
        (
            SerializedProperty property,
            string customFormat,
            string allowedCharacters,
            NumberStyles numberStyles
        ) {
            var loProp = property.FindPropertyRelative(SerializableDecimal.LoPartName);
            var midProp = property.FindPropertyRelative(SerializableDecimal.MidPartName);
            var hiProp = property.FindPropertyRelative(SerializableDecimal.HiPartName);
            var flagsProp = property.FindPropertyRelative(SerializableDecimal.FlagsPartName);

            var decimalField = new DecimalField
            (
                label: property.displayName,
                format: customFormat,
                allowedCharacters,
                numberStyles
            );
            decimalField.AddToClassList(DecimalField.alignedFieldUssClassName);

            var currentValue = GetDecimalValue(loProp, midProp, hiProp, flagsProp);
            decimalField.SetValueWithoutNotify(currentValue);

            decimalField.RegisterValueChangedCallback(@event =>
            {
                SetDecimalValue(loProp, midProp, hiProp, flagsProp, @event.newValue);
            });

            decimalField.TrackPropertyValue(loProp, _ => UpdateFieldValue());
            decimalField.TrackPropertyValue(midProp, _ => UpdateFieldValue());
            decimalField.TrackPropertyValue(hiProp, _ => UpdateFieldValue());
            decimalField.TrackPropertyValue(flagsProp, _ => UpdateFieldValue());

            return decimalField;

            void UpdateFieldValue()
            {
                var value = GetDecimalValue(loProp, midProp, hiProp, flagsProp);
                decimalField.SetValueWithoutNotify(value);
            }
        }

        private static VisualElement CreateSliderField
        (
            SerializedProperty property,
            DecimalRangeAttribute range,
            string customFormat,
            string allowedCharacters,
            NumberStyles numberStyles,
            bool integersOnly
        ) {
            var loProp = property.FindPropertyRelative(SerializableDecimal.LoPartName);
            var midProp = property.FindPropertyRelative(SerializableDecimal.MidPartName);
            var hiProp = property.FindPropertyRelative(SerializableDecimal.HiPartName);
            var flagsProp = property.FindPropertyRelative(SerializableDecimal.FlagsPartName);

            var root = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexGrow = 1 }
            };

            var slider = new Slider(start: range.Min, end: range.Max)
            {
                label = property.displayName,
                showInputField = false,
                style = { flexGrow = 1 }
            };
            slider.AddToClassList(Slider.alignedFieldUssClassName);

            var inputField = new DecimalField
            (
                label: string.Empty,
                format: customFormat,
                allowedCharacters,
                numberStyles
            ) {
                style = { minWidth = 60, marginLeft = 4 }
            };
            inputField.AddToClassList(DecimalField.alignedFieldUssClassName);

            root.Add(slider);
            root.Add(inputField);

            var currentValue = GetDecimalValue(loProp, midProp, hiProp, flagsProp);
            slider.SetValueWithoutNotify((float) currentValue);
            inputField.SetValueWithoutNotify(currentValue);

            slider.RegisterValueChangedCallback(@event =>
            {
                var clampedValue = (decimal) Mathf.Clamp(@event.newValue, range.Min, range.Max);
                if (integersOnly)
                {
                    clampedValue = Math.Round(clampedValue);
                }

                SetDecimalValue(loProp, midProp, hiProp, flagsProp, clampedValue);
                inputField.SetValueWithoutNotify(clampedValue);
            });
            inputField.RegisterValueChangedCallback(@event =>
            {
                var clampedValue = (decimal) Mathf.Clamp((float) @event.newValue, range.Min, range.Max);

                SetDecimalValue(loProp, midProp, hiProp, flagsProp, clampedValue);
                slider.SetValueWithoutNotify((float)clampedValue);
            });

            slider.TrackPropertyValue(loProp, _ => UpdateFieldValue());
            slider.TrackPropertyValue(midProp, _ => UpdateFieldValue());
            slider.TrackPropertyValue(hiProp, _ => UpdateFieldValue());
            slider.TrackPropertyValue(flagsProp, _ => UpdateFieldValue());

            return root;

            void UpdateFieldValue()
            {
                var value = GetDecimalValue(loProp, midProp, hiProp, flagsProp);
                slider.SetValueWithoutNotify((float) value);
                inputField.SetValueWithoutNotify(value);
            }
        }

        private static decimal GetDecimalValue
        (
            SerializedProperty loProp,
            SerializedProperty midProp,
            SerializedProperty hiProp,
            SerializedProperty flagsProp
        )
        {
            var flags = flagsProp.intValue;
            var isNegative = (flags & 0x80000000) != 0;
            var scale = (byte)((flags & 0x00FF0000) >> 16);

            return new decimal(loProp.intValue, midProp.intValue, hiProp.intValue, isNegative, scale);
        }

        private static void SetDecimalValue
        (
            SerializedProperty loProp,
            SerializedProperty midProp,
            SerializedProperty hiProp,
            SerializedProperty flagsProp,
            decimal value
        )
        {
            var bits = decimal.GetBits(value);

            loProp.intValue = bits[0];
            midProp.intValue = bits[1];
            hiProp.intValue = bits[2];
            flagsProp.intValue = bits[3];

            loProp.serializedObject.ApplyModifiedProperties();
        }
    }

    internal static class CheckAttributeExtensions
    {
        public static T Matches<T>(this object[] collection, T fallback) where T : class
        {
            return collection.FirstOrDefault(static attribute => attribute is T) as T
                ?? fallback;
        }
    }
}
