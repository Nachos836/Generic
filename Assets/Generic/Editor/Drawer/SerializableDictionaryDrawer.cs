#nullable enable

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Generic.Editor.Drawer
{
    using SerializableValueObjects;

    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    internal sealed class SerializableDictionaryDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var entriesProperty = property.FindPropertyRelative(nameof(SerializableDictionary<int,int>._entries));
            var entriesField = new PropertyField(entriesProperty, property.displayName);

            return entriesField;
        }
    }
}
