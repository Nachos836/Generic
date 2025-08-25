// ReSharper disable once CheckNamespace
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedParameter.Local

namespace System.Runtime.CompilerServices
{
    public class RequiredMemberAttribute : Attribute { }

    public class CompilerFeatureRequiredAttribute : Attribute
    {
        public CompilerFeatureRequiredAttribute(string name) { }
    }

    [AttributeUsage(AttributeTargets.Constructor)]
    public sealed class SetsRequiredMembersAttribute : Attribute { }
}
