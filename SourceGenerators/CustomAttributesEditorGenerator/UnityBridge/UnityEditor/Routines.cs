using UnityEngine.UIElements;

// ReSharper disable once CheckNamespace
namespace UnityEditor;

internal abstract class Editor
{
    // ReSharper disable once InconsistentNaming
    public object serializedObject { get; } = default!;

    public virtual VisualElement CreateInspectorGUI()
    {
        return null!;
    }
}
