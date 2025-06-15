#nullable enable

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SerializableValueObjects.Editor.Dictionary
{
    [Serializable]
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    internal sealed class SerializableDictionaryDrawer : PropertyDrawer
    {
        [SerializeField] private StyleSheet _style = default!;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var entriesProperty = property.FindPropertyRelative(nameof(SerializableDictionary<int,int>._entries));
            var entriesField = new PropertyField(entriesProperty, property.displayName);
            entriesField.styleSheets.Add(_style);

            return entriesField;
        }
    }
}
