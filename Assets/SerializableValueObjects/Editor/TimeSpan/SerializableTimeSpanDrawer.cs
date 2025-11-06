#nullable enable

using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace SerializableValueObjects.Editor.TimeSpan
{
    using Common;

    using static SerializableTimeSpan.Unit;

    [Serializable]
    [CustomPropertyDrawer(typeof(SerializableTimeSpan))]
    internal sealed class SerializableTimeSpanDrawer : PropertyDrawer
    {
        [SerializeField] private VisualTreeAsset _propertyGUI = default!;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var ticksProperty = property.FindPropertyRelative(nameof(SerializableTimeSpan._ticks));
            var unitProperty = property.FindPropertyRelative(nameof(SerializableTimeSpan._displayUnit));

            var container = _propertyGUI.CloneTree();
            var mainRowContainer = container.Q("Root");
            var propertyLabel = container.Q<Label>("BaseName");
            var labelContainer = container.Q<VisualElement>("LabelContainer");
            var currentTypeLabel = container.Q<Label>("TypeName");
            var inputField = container.Q<TextField>("InputField");
            var unitDropdown = container.Q<EnumField>("InputType");

            mainRowContainer.RegisterCallbackOnce<AttachToPanelEvent>(_ =>
            {
                propertyLabel.text = property.displayName;
                var currentUnit = (SerializableTimeSpan.Unit) unitProperty.enumValueIndex;
                unitDropdown.Init(currentUnit);
                currentTypeLabel.text = $"in {currentUnit}";
                inputField.value = System.TimeSpan.FromTicks(ticksProperty.longValue)
                    .ToUnitString((SerializableTimeSpan.Unit) unitProperty.enumValueIndex);
            });

            unitDropdown.RegisterValueChangedCallback(OnDropdownUnitChanged);
            inputField.RegisterValueChangedCallback(OnInputChanged);

            mainRowContainer.RegisterCallbackOnce<DetachFromPanelEvent>(_ =>
            {
                unitDropdown.UnregisterValueChangedCallback(OnDropdownUnitChanged);
                inputField.UnregisterValueChangedCallback(OnInputChanged);
            });

            mainRowContainer.RegisterCallback<GeometryChangedEvent, EditorLabelAutoAdjust>
            (
                static (_, resizer) => resizer.Adjust(),
                new EditorLabelAutoAdjust(mainRowContainer, labelContainer)
            );

            return mainRowContainer;

            void OnInputChanged(ChangeEvent<string> @event)
            {
                var currentUnit = (SerializableTimeSpan.Unit) unitProperty.enumValueIndex;

                if (decimal.TryParse(@event.newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var inputValue) is false)
                {
                    inputField.value = "0";
                }
                else
                {
                    if (currentUnit == Ticks)
                    {
                        if (inputValue < 0 || inputValue != Math.Truncate(inputValue))
                        {
                            inputField.value = System.TimeSpan.FromTicks((long) Math.Max(0, Math.Truncate(inputValue)))
                                .ToUnitString(currentUnit);
                        }
                        else
                        {
                            ticksProperty.longValue = (long) inputValue;
                            property.serializedObject.ApplyModifiedProperties();
                        }
                    }
                    else
                    {
                        if (inputValue < 0)
                        {
                            inputField.value = "0";
                        }
                        else
                        {
                            ticksProperty.longValue = ConvertUnitToTicks(inputValue, currentUnit);
                            property.serializedObject.ApplyModifiedProperties();
                        }
                    }
                }
            }

            void OnDropdownUnitChanged(ChangeEvent<Enum> @event)
            {
                var oldUnit = (SerializableTimeSpan.Unit) unitProperty.enumValueIndex;
                var newUnit = (SerializableTimeSpan.Unit) @event.newValue;

                if (decimal.TryParse(inputField.value, NumberStyles.Float, CultureInfo.InvariantCulture, out var inputValue))
                {
                    inputField.value = ConvertUnitToUnit(inputValue, oldUnit, newUnit)
                        .ToString(CultureInfo.InvariantCulture);
                }

                unitProperty.enumValueIndex = (int) newUnit;
                currentTypeLabel.text = $"in { newUnit }";
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        private static decimal ConvertUnitToUnit(decimal value, SerializableTimeSpan.Unit from, SerializableTimeSpan.Unit to)
        {
            var ticks = ConvertUnitToTicks(value, from);
            return ConvertTicksToUnit(ticks, to);
        }

        private static decimal ConvertTicksToUnit(decimal ticks, SerializableTimeSpan.Unit unit)
        {
            var timeSpan = System.TimeSpan.FromTicks((long) ticks);

            return unit switch
            {
                Ticks => ticks,
                Milliseconds => (decimal) timeSpan.TotalMilliseconds,
                Seconds => (decimal) timeSpan.TotalSeconds,
                Minutes => (decimal) timeSpan.TotalMinutes,
                Hours => (decimal) timeSpan.TotalHours,
                Days => (decimal) timeSpan.TotalDays,
                _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
            };
        }

        private static long ConvertUnitToTicks(decimal value, SerializableTimeSpan.Unit unit)
        {
            return unit switch
            {
                Ticks => (long) value,
                Milliseconds => System.TimeSpan.FromMilliseconds((double) value).Ticks,
                Seconds => System.TimeSpan.FromSeconds((double) value).Ticks,
                Minutes => System.TimeSpan.FromMinutes((double) value).Ticks,
                Hours => System.TimeSpan.FromHours((double) value).Ticks,
                Days => System.TimeSpan.FromDays((double) value).Ticks,
                _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
            };
        }
    }

    internal static class SerializableTimeSpanExtensions
    {
        public static string ToUnitString(this System.TimeSpan input, SerializableTimeSpan.Unit unit)
        {
            return unit switch
            {
                Ticks => input.Ticks.ToString(CultureInfo.InvariantCulture),
                Milliseconds => input.TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
                Seconds => input.TotalSeconds.ToString(CultureInfo.InvariantCulture),
                Minutes => input.TotalMinutes.ToString(CultureInfo.InvariantCulture),
                Hours => input.TotalHours.ToString(CultureInfo.InvariantCulture),
                Days => input.TotalDays.ToString(CultureInfo.InvariantCulture),
                _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
            };
        }
    }
}
