#nullable enable

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Generic.Editor.Drawer
{
    using SerializableValueObjects;

    [CustomPropertyDrawer(typeof(SerializableDictionary<,>.Entry))]
    internal sealed class SerializableDictionaryEntryDrawer : PropertyDrawer
    {
        private static readonly Color WarningColor = new (a: 0.6f, r:0.9f, g: 0.2f, b: 0.2f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var keyProperty = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._key));
            var valueProperty = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._value));
            var duplicateProp = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._duplicated));

            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart,
                    flexWrap = Wrap.Wrap,
                    marginLeft = -24
                }
            };

            var duplicateIndicator = new Image
            {
                image = EditorGUIUtility.IconContent("CollabConflict Icon").image,
                tooltip = "An element with the same key already exists in the dictionary.",
                style =
                {
                    alignSelf = Align.FlexStart,
                    alignContent = Align.Center,
                    width = 16,
                    height = 16,
                    marginTop = 2,
                    marginBottom = 2,
                    marginRight = 2
                },
                visible = duplicateProp.boolValue
            };
            var keyField = new PropertyField(keyProperty, string.Empty)
            {
                style =
                {
                    minWidth = Length.Percent(25),
                    maxWidth = Length.Percent(50),
                    marginRight = 4,
                    paddingRight = 4
                }
            };
            var valueField = new PropertyField(valueProperty, string.Empty)
            {
                style =
                {
                    flexGrow = 1
                }
            };

            container.Add(duplicateIndicator);
            container.Add(keyField);
            container.Add(valueField);

            SetPotentialWarningColor();

            keyField.RegisterValueChangeCallback(_ =>
            {
                property.serializedObject.Update();
                duplicateIndicator.visible = duplicateProp.boolValue;
                SetPotentialWarningColor();
            });

            return container;

            void SetPotentialWarningColor()
            {
                keyField.style.backgroundColor = duplicateProp.boolValue
                    ? new StyleColor { value = WarningColor }
                    : new StyleColor { keyword = StyleKeyword.Initial };
            }
        }
    }
}
