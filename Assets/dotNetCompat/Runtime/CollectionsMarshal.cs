#nullable enable

using System.Collections.Generic;
using System.Runtime.CompilerServices;

using static System.Runtime.CompilerServices.MethodImplOptions;
using static dotNetCompat.Extensions.Utilities;

// ReSharper disable CheckNamespace
namespace System.Runtime.InteropServices
{
    public static class CollectionsMarshal
    {
        [MethodImpl(AggressiveInlining)]
        public static Span<T> AsSpan<T>(List<T> list)
        {
            return new Span<T>(new ListCastHelper(list).GetArray<T>(), 0, list.Count);
        }
    }
}
