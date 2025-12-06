using System;
using System.Diagnostics;
using UnityEngine;

namespace InspectorAttributes
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class RequiredAttribute : PropertyAttribute { }
}
