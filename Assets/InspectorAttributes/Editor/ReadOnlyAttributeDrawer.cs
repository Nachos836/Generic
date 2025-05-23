﻿#nullable enable

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace InspectorAttributes.Editor
{
    [CustomPropertyDrawer(typeof(ReadOnlyAttribute))]
    internal sealed class ReadOnlyAttributeDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            return new PropertyField(property)
            {
                enabledSelf = false
            };
        }
    }
}
