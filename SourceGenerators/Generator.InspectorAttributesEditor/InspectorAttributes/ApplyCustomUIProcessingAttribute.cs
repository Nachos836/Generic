using System;

// ReSharper disable once CheckNamespace
namespace InspectorAttributes;

[AttributeUsage(validOn: AttributeTargets.Class)]
internal sealed class ApplyCustomUIProcessingAttribute : Attribute;
