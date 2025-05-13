using System;
using System.Diagnostics;

namespace InspectorAttributes
{
    [Obsolete("Not Yet Ready: disabled")]
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonAttribute : Attribute
    {
        internal readonly string Label;
        internal readonly string MethodName;

        public ButtonAttribute(string methodName)
        {
            Label = MethodName = methodName;
        }

        public ButtonAttribute(string label, string methodName) : this(methodName)
        {
            Label = label;
        }
    }
}
