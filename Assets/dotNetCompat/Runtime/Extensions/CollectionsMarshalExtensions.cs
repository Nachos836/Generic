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

        /// <summary>
        /// Returns a reference to the 0th element of <paramref name="array"/>. If the array is empty, returns a reference to where the 0th element
        /// would have been stored. Such a reference may be used for pinning but must never be dereferenced.
        /// </summary>
        /// <exception cref="NullReferenceException"><paramref name="array"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// This method does not perform array variance checks. The caller must manually perform any array variance checks
        /// if the caller wishes to write to the returned reference.
        /// </remarks>
        [MethodImpl(AggressiveInlining)]
        public static ref T GetArrayDataReference<T>(T[] array) => ref Unsafe.As<byte, T>(ref GetArrayDataReference(Unsafe.As<Array>(array)));

        /// <summary>
        /// Returns a reference to the 0th element of <paramref name="array"/>. If the array is empty, returns a reference to where the 0th element
        /// would have been stored. Such a reference may be used for pinning but must never be dereferenced.
        /// </summary>
        /// <exception cref="NullReferenceException"><paramref name="array"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The caller must manually reinterpret the returned <em>ref byte</em> as a ref to the array's underlying elemental type,
        /// perhaps utilizing an API such as <em>System.Runtime.CompilerServices.Unsafe.As</em> to assist with the reinterpretation.
        /// This technique does not perform array variance checks. The caller must manually perform any array variance checks
        /// if the caller wishes to write to the returned reference.
        /// </remarks>
        [MethodImpl(AggressiveInlining)]
        public static unsafe ref byte GetArrayDataReference(Array array)
        {
            // If needed, we can save one or two instructions per call by marking this method as intrinsic and asking the JIT
            // to special-case arrays of a known type and dimension.

            // See comment on RawArrayData (in RuntimeHelpers.CoreCLR.cs) for details
            return ref Unsafe.AddByteOffset(ref Unsafe.As<RawData>(array).Data, RuntimeHelpersCustomExtensions.GetMethodTable(array)->BaseSize - (nuint)(2 * sizeof(IntPtr)));
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
