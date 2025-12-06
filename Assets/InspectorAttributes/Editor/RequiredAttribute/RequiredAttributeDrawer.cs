#nullable enable

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEngine;

namespace InspectorAttributes.Editor.RequiredAttribute
{
    [CustomPropertyDrawer(typeof(InspectorAttributes.RequiredAttribute))]
    public class RequiredAttributeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            var propertyField = new PropertyField(property);

            root.Add(propertyField);

            Validate(SerializedPropertyChangeEvent.GetPooled(property));

            propertyField.RegisterValueChangeCallback(Validate);

            return root;

            void Validate(SerializedPropertyChangeEvent @event)
            {
                var isMissing = IsValueMissing(@event.changedProperty);

                // Красим фон в полупрозрачный красный, если значения нет
                propertyField.style.backgroundColor = isMissing
                    ? new StyleColor(new Color(1f, 0f, 0f, 0.15f))
                    : new StyleColor(StyleKeyword.Null);

                // Добавляем красную рамку для заметности
                propertyField.style.borderTopColor = isMissing ? Color.red : new StyleColor(StyleKeyword.Null);
                propertyField.style.borderBottomColor = isMissing ? Color.red : new StyleColor(StyleKeyword.Null);
                propertyField.style.borderLeftColor = isMissing ? Color.red : new StyleColor(StyleKeyword.Null);
                propertyField.style.borderRightColor = isMissing ? Color.red : new StyleColor(StyleKeyword.Null);
                propertyField.style.borderTopWidth = isMissing ? 1 : 0;
                propertyField.style.borderBottomWidth = isMissing ? 1 : 0;
                propertyField.style.borderLeftWidth = isMissing ? 1 : 0;
                propertyField.style.borderRightWidth = isMissing ? 1 : 0;
            }
        }

        private static bool IsValueMissing(SerializedProperty property)
        {
            return property.propertyType switch
            {
                SerializedPropertyType.ObjectReference => property.objectReferenceValue == null,
                SerializedPropertyType.String => string.IsNullOrEmpty(property.stringValue),
                _ => false
            };
        }
    }
}
