using System;

// ReSharper disable once CheckNamespace
namespace InspectorAttributes;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ButtonAttribute : Attribute
{
    internal readonly string Label;

    public ButtonAttribute(string label)
    {
        Label = label;
    }
}
