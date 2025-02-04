#nullable enable

using UnityEditor;

namespace Generic.Editor.Drawer
{
    using SerializableValueObjects;

    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), useForChildren: true)]
    internal sealed class SerializableDictionaryDrawer : PropertyDrawer
    {
        // TBD
        // Prevents displaying in Unity
    }
}
