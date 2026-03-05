using System;
using System.Diagnostics;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace InspectorAttributes
{
    [Conditional("UNITY_EDITOR")]
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class InspectableReferenceAttribute : PropertyAttribute { }
}
