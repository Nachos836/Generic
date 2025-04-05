using System;
using System.Diagnostics;
using UnityEngine;

namespace InspectorAttributes
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ButtonAttribute : PropertyAttribute
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
