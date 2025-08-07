using System;
using System.Diagnostics;

namespace InspectorAttributes
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonAttribute : Attribute
    {
        internal readonly string Label;

        public ButtonAttribute(string label)
        {
            Label = label;
        }
    }
}
