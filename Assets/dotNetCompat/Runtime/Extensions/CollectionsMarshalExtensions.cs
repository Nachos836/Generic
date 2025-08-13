using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static System.Runtime.CompilerServices.MethodImplOptions;
using static System.Runtime.InteropServices.LayoutKind;
using static dotNetCompat.Extensions.Utilities;

namespace dotNetCompat.Extensions
{
    /// <summary>
    /// This API extends classic .net API
    /// </summary>
    public static class CollectionsMarshalExtensions
    {
        [MethodImpl(AggressiveInlining)]
        public static ReadOnlySpan<T> AsReadOnlySpan<T>(this List<T> list)
        {
            return new ReadOnlySpan<T>(new ListCastHelper(list).GetArray<T>(), 0, list.Count);
        }
    }

    internal static class Utilities
    {
        [StructLayout(Explicit)]
        [SuppressMessage("ReSharper", "PrivateFieldCanBeConvertedToLocalVariable")]
        public readonly ref struct ListCastHelper
        {
            [FieldOffset(0)] private readonly object _list;
            [FieldOffset(0)] private readonly StrongBox<Array> _strongBox;

            [MethodImpl(AggressiveInlining)]
            public T[] GetArray<T>() => (T[]) _strongBox.Value;

            public ListCastHelper(IList list)
            {
                _strongBox = default!;
                _list = list;
            }
        }
    }
}
