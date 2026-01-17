// ReSharper disable once CheckNamespace
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedParameter.Local

// Members MUST be public (so other assemblies could reach members)
// It's not the only meant to be using by CLR
namespace System.Runtime.CompilerServices
{
    public sealed class RequiredMemberAttribute : Attribute { }

    public sealed class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string name) { }
    }
}

namespace System.Diagnostics.CodeAnalysis
{
    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class SetsRequiredMembersAttribute : Attribute { }
}
