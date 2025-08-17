using System;
using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace InspectorAttributes;

[Conditional("UNITY_EDITOR")]
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ButtonAttribute : Attribute
{
    internal readonly string Label;

    public ButtonAttribute(string label)
    {
        Label = label;
    }
}
