#if NETSTANDARD2_0

// ReSharper disable CheckNamespace

namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class IsByRefLikeAttribute : Attribute;
}

#endif
