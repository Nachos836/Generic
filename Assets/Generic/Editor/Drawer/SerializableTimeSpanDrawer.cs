using System;
using System.Globalization;
using UnityEditor;
using UnityEngine.UIElements;

using static Generic.SerializableValueObjects.SerializableTimeSpan.Unit;

namespace Generic.Editor.Drawer
{
    using SerializableValueObjects;

    [CustomPropertyDrawer(typeof(SerializableTimeSpan))]
    internal sealed class SerializableTimeSpanDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var ticksProperty = property.FindPropertyRelative(nameof(SerializableTimeSpan._ticks));
            var unitProperty = property.FindPropertyRelative(nameof(SerializableTimeSpan._displayUnit));

            var mainRowContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.SpaceEvenly
                }
            };
            var labelContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.FlexStart,
                    minWidth = Length.Percent(50),
                    marginLeft = 4,
                    flexShrink = 0
                }
            };
            var inputContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    justifyContent = Justify.FlexEnd,
                    minWidth = Length.Percent(50),
                    flexShrink = 0
                }
            };

            var propertyLabel = new Label(property.displayName);
            var currentTypeLabel = new Label(text: $"in {(SerializableTimeSpan.Unit) unitProperty.enumValueIndex}");

            labelContainer.Add(propertyLabel);
            labelContainer.Add(currentTypeLabel);
            mainRowContainer.Add(labelContainer);

            var inputField = new TextField
            {
                label = null,
                style = { flexGrow = 1, marginRight = 8 }
            };

            var unitDropdown = new EnumField((SerializableTimeSpan.Unit) unitProperty.enumValueIndex)
            {
                style = { flexShrink = 1 }
            };

            mainRowContainer.RegisterCallbackOnce<AttachToPanelEvent>(_ =>
            {
                currentTypeLabel.text = $"in {(SerializableTimeSpan.Unit) unitProperty.enumValueIndex}";
                inputField.value = TimeSpan.FromTicks(ticksProperty.longValue)
                    .ToUnitString((SerializableTimeSpan.Unit) unitProperty.enumValueIndex);
            });

            unitDropdown.RegisterValueChangedCallback(OnDropdownUnitChanged);
            inputField.RegisterValueChangedCallback(OnInputChanged);

            inputContainer.Add(inputField);
            inputContainer.Add(unitDropdown);
            mainRowContainer.Add(inputContainer);

            mainRowContainer.RegisterCallbackOnce<DetachFromPanelEvent>(_ =>
            {
                unitDropdown.UnregisterValueChangedCallback(OnDropdownUnitChanged);
                inputField.UnregisterValueChangedCallback(OnInputChanged);
            });

            return mainRowContainer;

            void OnInputChanged(ChangeEvent<string> evt)
            {
                var currentUnit = (SerializableTimeSpan.Unit) unitProperty.enumValueIndex;

                if (decimal.TryParse(evt.newValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var inputValue) is false)
                {
                    inputField.value = "0";
                }
                else
                {
                    if (currentUnit == Ticks)
                    {
                        if (inputValue < 0 || inputValue != Math.Truncate(inputValue))
                        {
                            inputField.value = TimeSpan.FromTicks((long) Math.Max(0, Math.Truncate(inputValue)))
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
            var timeSpan = TimeSpan.FromTicks((long) ticks);

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
                Milliseconds => TimeSpan.FromMilliseconds((double) value).Ticks,
                Seconds => TimeSpan.FromSeconds((double) value).Ticks,
                Minutes => TimeSpan.FromMinutes((double) value).Ticks,
                Hours => TimeSpan.FromHours((double) value).Ticks,
                Days => TimeSpan.FromDays((double) value).Ticks,
                _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null)
            };
        }


    }

    internal static class SerializableTimeSpanExtensions
    {
        public static string ToUnitString(this TimeSpan input, SerializableTimeSpan.Unit unit)
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
