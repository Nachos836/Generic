using System;

namespace InspectorAttributes;

[AttributeUsage(validOn: AttributeTargets.Class)]
internal sealed class ApplyCustomUIProcessingAttribute : Attribute;
