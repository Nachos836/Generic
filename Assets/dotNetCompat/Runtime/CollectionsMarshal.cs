#nullable enable

using System.Collections.Generic;
using System.Runtime.CompilerServices;

// ReSharper disable CheckNamespace
namespace System.Runtime.InteropServices
{
    public static class CollectionsMarshal
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(List<T> list)
        {
            var box = new ListCastHelper { List = list }.StrongBox!;

            return new Span<T>((T[]) box.Value, 0, list.Count);
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct ListCastHelper
        {
            [FieldOffset(0)]
            public StrongBox<Array> StrongBox;

            [FieldOffset(0)]
            public object List;
        }
    }
}
