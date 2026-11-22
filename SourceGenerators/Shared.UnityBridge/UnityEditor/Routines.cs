using UnityEngine.UIElements;

namespace UnityEditor;

internal abstract class Editor
{
    // ReSharper disable once InconsistentNaming
    public object serializedObject { get; } = default!;

    // ReSharper disable once InconsistentNaming
    public object target { get; } = default!;

    public virtual VisualElement CreateInspectorGUI()
    {
        return null!;
    }
}

[System.AttributeUsage(System.AttributeTargets.Class)]
internal sealed class CustomEditor : System.Attribute
{
    public CustomEditor(System.Type inspectedType) { }
}
