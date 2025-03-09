using UnityEditor;
using UnityEngine;

namespace Generic.Editor.Drawer
{
    using SerializableValueObjects;

    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    internal sealed class SerializableDictionaryDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var prop = property.FindPropertyRelative(nameof(SerializableDictionary<int,int>._entries));
            EditorGUI.PropertyField(position, prop, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var prop = property.FindPropertyRelative(nameof(SerializableDictionary<int,int>._entries));
            return EditorGUI.GetPropertyHeight(prop);
        }
    }
}
