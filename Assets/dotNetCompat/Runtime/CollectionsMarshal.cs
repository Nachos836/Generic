// Credits to https://github.com/hacbit/CollectionsMarshalForUnity/blob/main/CollectionsMarshal.cs

#if NET6_0_OR_GREATER
[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(System.Runtime.InteropServices.CollectionsMarshal))]
#else

#nullable enable

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

// ReSharper disable All
#pragma warning disable CS0649 // Field is never assigned to, and will always have its default value
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

// ReSharper disable CheckNamespace
namespace System.Runtime.InteropServices
{
    /// <summary>
    /// An unsafe class that provides a set of methods to access the underlying data representations of collections.
    /// </summary>
    public static class CollectionsMarshal
    {
        /// <summary>
        /// Get a <see cref="Span{T}"/> view over a <see cref="List{T}"/>'s data.
        /// Items should not be added or removed from the <see cref="List{T}"/> while the <see cref="Span{T}"/> is in use.
        /// </summary>
        /// <param name="list">The list to get the data view over.</param>
        /// <typeparam name="T">The type of the elements in the list.</typeparam>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Span<T> AsSpan<T>(List<T>? list)
        {
            Span<T> span = default;
            if (list is null) return span;

            var listData = Unsafe.As<List<T>, ListDataHelper<T>>(ref list);
            var size = listData.Size;
            var items = listData.Items;
            Debug.Assert(items is not null, "Implementation depends on List<T> always having an array.");

            if ((uint) size > (uint) items.Length)
            {
                // List<T> was erroneously mutated concurrently with this call, leading to a count larger than its array.
                throw new InvalidOperationException("Concurrent operations are not supported.");
            }

            Debug.Assert(typeof(T[]) == items.GetType(), "Implementation depends on List<T> always using a T[] and not U[] where U : T.");
            span = new Span<T>(items, 0, size);

            return span;
        }

        /// <summary>
        /// Gets either a ref to a <typeparamref name="TValue"/> in the <see cref="Dictionary{TKey, TValue}"/> or a ref null if it does not exist in the <paramref name="dictionary"/>.
        /// </summary>
        /// <param name="dictionary">The dictionary to get the ref to <typeparamref name="TValue"/> from.</param>
        /// <param name="key">The key used for lookup.</param>
        /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
        /// <remarks>
        /// Items should not be added or removed from the <see cref="Dictionary{TKey, TValue}"/> while the ref <typeparamref name="TValue"/> is in use.
        /// The ref null can be detected using System.Runtime.CompilerServices.Unsafe.IsNullRef
        /// </remarks>
        public static ref TValue? GetValueRefOrNullRef<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key) where TKey : notnull
            => ref dictionary.FindValue(key);

        /// <summary>
        /// Gets a ref to a <typeparamref name="TValue"/> in the <see cref="Dictionary{TKey, TValue}"/>, adding a new entry with a default value if it does not exist in the <paramref name="dictionary"/>.
        /// </summary>
        /// <param name="dictionary">The dictionary to get the ref to <typeparamref name="TValue"/> from.</param>
        /// <param name="key">The key used for lookup.</param>
        /// <param name="exists">Whether a new entry for the given key was added to the dictionary.</param>
        /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
        /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
        /// <remarks>Items should not be added to or removed from the <see cref="Dictionary{TKey, TValue}"/> while the ref <typeparamref name="TValue"/> is in use.</remarks>
        public static ref TValue? GetValueRefOrAddDefault<TKey, TValue>(Dictionary<TKey, TValue> dictionary, TKey key, out bool exists) where TKey : notnull
            => ref CollectionsMarshalHelper<TKey, TValue>.GetValueRefOrAddDefault(dictionary, key, out exists);

        /// <summary>
        /// Sets the count of the <see cref="List{T}"/> to the specified value.
        /// </summary>
        /// <param name="list">The list to set the count of.</param>
        /// <param name="count">The value to set the list's count to.</param>
        /// <typeparam name="T">The type of the elements in the list.</typeparam>
        /// <exception cref="NullReferenceException">
        /// <paramref name="list"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="count"/> is negative.
        /// </exception>
        /// <remarks>
        /// When increasing the count, uninitialized data is being exposed.
        /// </remarks>
        public static void SetCount<T>(List<T> list, int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Non-negative number required.");
            }

            // list._version++;
            ref var listData = ref Unsafe.As<List<T>, ListDataHelper<T>>(ref list);
            ref var version = ref listData.Version;
            version++;

            ref var items = ref listData.Items;
            ref var size = ref listData.Size;

            if (count > list.Capacity)
            {
                list.Grow(count);
            }
            else if (count < /* list._size */ size && RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                Array.Clear(/* list._items */ items, count, /* list._size */ size - count);
            }

            // list._size = count;
            size = count;
        }
    }

    internal static class ListExtensions
    {
        /// <summary>
        /// Increase the capacity of this list to at least the specified <paramref name="capacity"/>.
        /// </summary>
        /// <param name="list">Income list</param>
        /// <param name="capacity">The minimum capacity to ensure.</param>
        internal static void Grow<T>(this List<T> list, int capacity)
        {
            list.Capacity = list.GetNewCapacity(capacity);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetNewCapacity<T>(this List<T> list, int capacity)
        {
            const int defaultCapacity = 4;

            var listData = Unsafe.As<List<T>, ListDataHelper<T>>(ref list);
            var items = listData.Items;
            Debug.Assert(items.Length < capacity);

            var newCapacity = items.Length == 0 ? defaultCapacity : 2 * items.Length;

            // Allow the list to grow to the maximum possible capacity (~2G elements) before encountering overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint)newCapacity > /* Array.MaxLength */ 0X7FFFFFC7) newCapacity = /* Array.MaxLength */ 0X7FFFFFC7;

            // If the computed capacity is still less than specified, set to the original argument.
            // Capacities exceeding Array.MaxLength will be surfaced as OutOfMemoryException by Array.Resize.
            if (newCapacity < capacity) newCapacity = capacity;

            return newCapacity;
        }
    }

    internal static class DictionaryExtensions
    {
        internal static ref TValue? FindValue<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            ref var entry = ref Unsafe.NullRef<Entry<TKey, TValue>>();
            ref var dictionary = ref Unsafe.As<Dictionary<TKey, TValue>, DictionaryDataHelper<TKey, TValue>>(ref dict);
            ref var buckets = ref dictionary.Buckets;
            ref var entries = ref dictionary.Entries;
            ref var comparer = ref dictionary.Comparer;

            if (!ReferenceEquals(buckets, null))
            {
                Debug.Assert(entries is not null, "expected entries to be non-null");
                var currentComparer = comparer;
                if (typeof(TKey).IsValueType && // comparer can only be null for value types; enable JIT to eliminate the entire if block for ref types
                    ReferenceEquals(currentComparer, null))
                {
                    var hashCode = (uint)key.GetHashCode();
                    var i = GetBucket(ref dictionary, hashCode);
                    var currentEntries = entries;
                    uint collisionCount = 0;

                    // ValueType: Devirtualize with EqualityComparer<TKey>.Default intrinsic
                    i--; // Value in _buckets is 1-based; subtract 1 from 'i'. We do it here so it fuses with the following conditional.
                    do
                    {
                        // Test in if to drop range check for the following array access
                        if ((uint) i >= (uint) currentEntries.Length)
                        {
                            goto ReturnNotFound;
                        }

                        entry = ref currentEntries[i];
                        if (entry.HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(entry.Key, key))
                        {
                            goto ReturnFound;
                        }

                        i = entry.Next;

                        collisionCount++;
                    } while (collisionCount <= (uint) currentEntries.Length);

                    // The chain of entries forms a loop, which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    goto ConcurrentOperation;
                }
                else
                {
                    Debug.Assert(comparer is not null);
                    var hashCode = (uint)comparer.GetHashCode(key);
                    var i = GetBucket(ref dictionary, hashCode);
                    var currentEntries = entries;
                    uint collisionCount = 0;
                    i--; // Value in _buckets is 1-based; subtract 1 from 'i'. We do it here so it fuses with the following conditional.
                    do
                    {
                        // Test in if to drop range check for the following array access
                        if ((uint)i >= (uint) currentEntries.Length)
                        {
                            goto ReturnNotFound;
                        }

                        entry = ref currentEntries[i];
                        if (entry.HashCode == hashCode && comparer.Equals(entry.Key, key))
                        {
                            goto ReturnFound;
                        }

                        i = entry.Next;

                        collisionCount++;
                    } while (collisionCount <= (uint) currentEntries.Length);

                    // The chain of entries forms a loop, which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    goto ConcurrentOperation;
                }
            }

            goto ReturnNotFound;

        ConcurrentOperation:
            throw new InvalidOperationException("Concurrent operations are not supported.");
        ReturnFound:
            ref var value = ref entry.Value;
        Return:
            return ref value!;
        ReturnNotFound:
            value = ref Unsafe.NullRef<TValue>()!;
            goto Return;
        }

        internal static ref int GetBucket<TKey, TValue>(ref DictionaryDataHelper<TKey, TValue> helper, uint hashCode)
        {
            var buckets = helper.Buckets;
            return ref buckets[hashCode % buckets.Length];
        }

        internal static int Initialize<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, int capacity)
        {
            var size = HashHelpers.GetPrime(capacity);
            var buckets = new int[size];
            var entries = new Entry<TKey, TValue>[size];

            ref var dictionaryData = ref Unsafe.As<Dictionary<TKey, TValue>, DictionaryDataHelper<TKey, TValue>>(ref dictionary);
            dictionaryData.Buckets = buckets;
            dictionaryData.Entries = entries;
            dictionaryData.FreeList = -1;
            return size;
        }

        internal static void Resize<TKey, TValue>(ref DictionaryDataHelper<TKey, TValue> dictionary)
        {
            Resize(ref dictionary, HashHelpers.ExpandPrime(dictionary.Count), false);
        }

        internal static void Resize<TKey, TValue>(ref DictionaryDataHelper<TKey, TValue> dictionary, int newSize, bool forceNewHashCodes)
        {
            var numArray = new int[newSize];
            for (var index = 0; index < numArray.Length; ++index)
            {
                numArray[index] = -1;
            }

            var destinationArray = new Entry<TKey, TValue>[newSize];

            Unsafe.CopyBlock(
                destination: ref Unsafe.As<Entry<TKey, TValue>, byte>(ref destinationArray[0]),
                source: ref Unsafe.As<Entry<TKey, TValue>, byte>(ref dictionary.Entries[0]),
                byteCount: (uint)(Unsafe.SizeOf<Entry<TKey, TValue>>() * dictionary.Count));
            if (forceNewHashCodes)
            {
                for (var index = 0; index < dictionary.Count; ++index)
                {
                    if ((int)destinationArray[index].HashCode != -1)
                        destinationArray[index].HashCode = (uint)dictionary.Comparer.GetHashCode(destinationArray[index].Key) & int.MaxValue;
                }
            }
            for (var index1 = 0; index1 < dictionary.Count; ++index1)
            {
                if (unchecked((int) destinationArray[index1].HashCode) < 0) continue;
                var index2 = (int)(destinationArray[index1].HashCode % newSize);
                destinationArray[index1].Next = numArray[index2];
                numArray[index2] = index1;
            }
            dictionary.Buckets = numArray;
            dictionary.Entries = destinationArray;
        }
    }

    internal static class HashHelpers
    {
        public const uint HashCollisionThreshold = 100;

        // This is the maximum prime smaller than Array.MaxLength.
        private const int MaxPrimeArrayLength = 0x7FFFFFC3;

        private const int HashPrime = 101;

        // Table of prime numbers to use as hash table sizes.
        // A typical resize algorithm would pick the smallest prime number in this array
        // that is larger than twice the previous capacity.
        // Suppose our Hashtable currently has capacity x and enough elements are added
        // such that a resize needs to occur. Resizing first computes 2x then finds the
        // first prime in the table greater than 2x, i.e., if primes are ordered
        // p_1, p_2, ..., p_i, ..., it finds p_n such that p_n-1 < 2x < p_n.
        // Doubling is important for preserving the asymptotic complexity of the
        // hashtable operations such as adding. Having prime guarantees that double
        // hashing does not lead to infinite loops. IE, your hash function will be
        // h1(key) + i*h2(key), 0 <= i < size. h2 and the size must be relatively prime.
        // We prefer the low computation costs of higher prime numbers over the increased
        // memory allocation of a fixed prime number, i.e., when right-sizing a HashSet.
        private static ReadOnlySpan<int> Primes => new[]
        {
            3, 7, 11, 17, 23, 29, 37, 47, 59, 71, 89, 107, 131, 163, 197, 239, 293, 353, 431, 521, 631, 761, 919,
            1103, 1327, 1597, 1931, 2333, 2801, 3371, 4049, 4861, 5839, 7013, 8419, 10103, 12143, 14591,
            17519, 21023, 25229, 30293, 36353, 43627, 52361, 62851, 75431, 90523, 108631, 130363, 156437,
            187751, 225307, 270371, 324449, 389357, 467237, 560689, 672827, 807403, 968897, 1162687, 1395263,
            1674319, 2009191, 2411033, 2893249, 3471899, 4166287, 4999559, 5999471, 7199369
        };

        private static bool IsPrime(int candidate)
        {
            if ((candidate & 1) == 0) return candidate == 2;

            var limit = (int) Math.Sqrt(candidate);
            for (var divisor = 3; divisor <= limit; divisor += 2)
            {
                if (candidate % divisor == 0) return false;
            }
            return true;
        }

        public static int GetPrime(int min)
        {
            if (min < 0) throw new ArgumentException("Capacity overflow");

            foreach (var prime in Primes)
            {
                if (prime >= min) return prime;
            }

            // The outside of our predefined table. Compute the hard way.
            for (var i = min | 1; i < int.MaxValue; i += 2)
            {
                if (IsPrime(i) && (i - 1) % HashPrime != 0) return i;
            }
            return min;
        }

        // Returns size of hashtable to grow to.
        public static int ExpandPrime(int oldSize)
        {
            var newSize = 2 * oldSize;

            // Allow the hashtables to grow to the maximum possible size (~2G elements) before encountering capacity overflow.
            // Note that this check works even when _items.Length overflowed thanks to the (uint) cast
            if ((uint)newSize <= MaxPrimeArrayLength || MaxPrimeArrayLength <= oldSize) return GetPrime(newSize);

            Debug.Assert(MaxPrimeArrayLength == GetPrime(MaxPrimeArrayLength), "Invalid MaxPrimeArrayLength");
            return MaxPrimeArrayLength;

        }
    }

    /// <summary>
    /// A helper class containing APIs exposed through <see cref="CollectionsMarshal"/> or <see cref="CollectionExtensions"/>.
    /// These methods are relatively niche and only used in specific scenarios, so adding them in a separate type avoids
    /// the additional overhead on each <see cref="Dictionary{TKey, TValue}"/> instantiation, especially in AOT scenarios.
    /// </summary>
    internal static class CollectionsMarshalHelper<TKey, TValue>
    {
        /// <inheritdoc cref="CollectionsMarshal.GetValueRefOrAddDefault{TKey, TValue}(Dictionary{TKey, TValue}, TKey, out bool)"/>
        public static ref TValue? GetValueRefOrAddDefault(Dictionary<TKey, TValue> dict, TKey key, out bool exists)
        {
            // NOTE: this method is mirrored by the Dictionary<TKey, TValue>.TryInsert above.
            // If you make any changes here, make sure to keep that version in sync as well.

            if (key == null) throw new ArgumentNullException(nameof(key));

            ref var dictionary = ref Unsafe.As<Dictionary<TKey, TValue>, DictionaryDataHelper<TKey, TValue>>(ref dict);

            if (ReferenceEquals(dictionary.Buckets, null))
            {
                dict.Initialize(0);
            }

            ref var entries = ref dictionary.Entries;

            var comparer = dictionary.Comparer;
            var hashCode = (uint)(typeof(TKey).IsValueType && ReferenceEquals(comparer, null)
                ? key.GetHashCode()
                : comparer.GetHashCode(key));

            var collisionCount = 0u;
            ref var bucket = ref DictionaryExtensions.GetBucket(ref dictionary, hashCode);
            var i = bucket - 1; // Value in _buckets is 1-based

            if (typeof(TKey).IsValueType && // comparer can only be null for value types; enable JIT to eliminate the entire if block for ref types
                comparer == null)
            {
                // ValueType: Devirtualize with EqualityComparer<TKey>.Default intrinsic
                while ((uint) i < (uint) entries.Length)
                {
                    if (entries[i].HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(entries[i].Key, key))
                    {
                        exists = true;

                        return ref entries[i].Value!;
                    }

                    i = entries[i].Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                    {
                        // The chain of entries forms a loop, which means a concurrent update has happened.
                        // Break out of the loop and throw, rather than looping forever.
                        throw new InvalidOperationException("Concurrent operations are not supported.");
                    }
                }
            }
            else if (comparer != null)
            {
                while ((uint) i < (uint) entries.Length)
                {
                    if (entries[i].HashCode == hashCode && comparer.Equals(entries[i].Key, key))
                    {
                        exists = true;

                        return ref entries[i].Value!;
                    }

                    i = entries[i].Next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Length)
                    {
                        // The chain of entries forms a loop, which means a concurrent update has happened.
                        // Break out of the loop and throw, rather than looping forever.
                        throw new InvalidOperationException("Concurrent operations are not supported.");
                    }
                }
            }

            const int startOfFreeList = -3;

            int index;
            if (dictionary.FreeCount > 0)
            {
                index = dictionary.FreeList;
                dictionary.FreeList = startOfFreeList - entries[dictionary.FreeList].Next;
                dictionary.FreeCount--;
            }
            else
            {
                var count = dictionary.Count;
                if (count == entries.Length)
                {
                    DictionaryExtensions.Resize(ref dictionary);
                    bucket = ref DictionaryExtensions.GetBucket(ref dictionary, hashCode);
                }
                index = count;
                dictionary.Count = count + 1;
                entries = dictionary.Entries;
            }

            ref var entry = ref entries[index];

            entry.HashCode = hashCode;
            entry.Next = bucket - 1; // Value in _buckets is 1-based
            entry.Key = key;
            entry.Value = default!;
            bucket = index + 1; // Value in _buckets is 1-based
            dictionary.Version++;

            // Value types never rehash
            if (!typeof(TKey).IsValueType && collisionCount > HashHelpers.HashCollisionThreshold /* && comparer is NonRandomizedStringEqualityComparer */)
            {
                // If we hit the collision threshold, we'll need to switch to the comparer which is using randomized string hashing
                // i.e., EqualityComparer<string>.Default.
                DictionaryExtensions.Resize(ref dictionary, entries.Length, true);

                exists = false;

                // At this point the entry array has been resized, so the current reference we have is no longer valid.
                // We're forced to do a new lookup and return an updated reference to the new entry instance. This new
                // lookup is guaranteed to always find a value, though, and it will never return a null reference here.
                ref TValue? value = ref dict.FindValue(key)!;

                Debug.Assert(!Unsafe.IsNullRef(ref value), "the lookup result cannot be a null ref here");

                return ref value;
            }

            exists = false;

            return ref entry.Value!;
        }
    }

    internal sealed record ListDataHelper<T>
    {
        public T[] Items;
        public int Size;
        public int Version;
    }

    internal sealed record DictionaryDataHelper<TKey, TValue>
    {
        public int[] Buckets;
        public Entry<TKey, TValue>[] Entries;
        public int Count;
        public int FreeList;
        public int FreeCount;
        public int Version;
        public IEqualityComparer<TKey> Comparer;
        public Dictionary<TKey, TValue>.KeyCollection Keys;
        public Dictionary<TKey, TValue>.ValueCollection Values;
        public object SyncRoot;
    }

    internal struct Entry<TKey, TValue>
    {
        public uint HashCode;
        /// <summary>
        /// 0-based index of the next entry in a chain: -1 means the end of a chain
        /// also encodes whether this entry _itself_ is part of the free list by changing sign and subtracting 3,
        /// so -2 means end of a free list, -3 means index 0 but on free list, -4 means index 1 but on free list, etc.
        /// </summary>
        public int Next;
        public TKey Key;     // Key of entry
        public TValue Value; // Value of entry
    }

    // Helper class to help with unsafe pinning of arbitrary objects.
    // It's used by VM code.
    internal sealed class RawData
    {
        public byte Data;
    }

    /// <summary>
    /// Extends the RuntimeHelpers
    /// </summary>
    internal static class RuntimeHelpersCustomExtensions
    {
        private static ref byte GetRawData(this object obj) => ref Unsafe.As<RawData>(obj).Data;

        // Given an object reference, returns its MethodTable*.
        //
        // WARNING: The caller has to ensure that MethodTable* does not get unloaded. The most robust way
        // to achieve this is by using GC.KeepAlive on the object that the MethodTable* was fetched from, e.g.:
        //
        // MethodTable* pMT = GetMethodTable(o);
        //
        // ... work with pMT ...
        //
        // GC.KeepAlive(o);
        //
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe MethodTable* GetMethodTable(object obj)
        {
            // The body of this function will be replaced by the EE with unsafe code
            // See getILIntrinsicImplementationForRuntimeHelpers for how this happens.

            return (MethodTable*)Unsafe.Add(ref Unsafe.As<byte, IntPtr>(ref obj.GetRawData()), -1);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal unsafe struct MethodTable
    {
        /// <summary>
        /// The low WORD of the first field is the component size for array and string types.
        /// </summary>
        [FieldOffset(0)]
        public ushort ComponentSize;

        /// <summary>
        /// The flags for the current method table (only for not array or string types).
        /// </summary>
        [FieldOffset(0)]
        private uint Flags;

        /// <summary>
        /// The base size of the type (used when allocating an instance on the heap).
        /// </summary>
        [FieldOffset(4)]
        public uint BaseSize;

        // See additional native members in "methodtable.h", not needed here yet.
        // 0x8: m_dwFlags2 (additional flags and token in the upper 24 bits)
        // 0xC: m_wNumVirtuals

        /// <summary>
        /// The number of interfaces implemented by the current type.
        /// </summary>
        [FieldOffset(0x0E)]
        public ushort InterfaceCount;

        // For DEBUG builds, there is a conditional field here (see "methodtable.h" again).
        // 0x10: debug_m_szClassName (display name of the class, for the debugger)

        /// <summary>
        /// A pointer to the parent method table for the current one.
        /// </summary>
        [FieldOffset(ParentMethodTableOffset)]
        public MethodTable* ParentMethodTable;

        // Additional conditional fields (see methodtable.h).
        // m_pModule
        // m_pAuxiliaryData
        // union {
        //   m_pEEClass (pointer to the EE class)
        //   m_pCanonMT (pointer to the canonical method table)
        // }

        /// <summary>
        /// This element type handle is in a union with additional info or a pointer to the interface map.
        /// Which one is used is based on the specific method table being used (so this field is not
        /// always guaranteed to actually be a pointer to a type handle for the element type of this type).
        /// </summary>
        [FieldOffset(ElementTypeOffset)]
        public void* ElementType;

        /// <summary>
        /// This interface map used to list out the set of interfaces. Only meaningful if InterfaceCount is non-zero.
        /// </summary>
        [FieldOffset(InterfaceMapOffset)]
        public MethodTable** InterfaceMap;

        // WFLAGS_LOW_ENUM
        private const uint EnumFlagGenericsMask = 0x00000030;
        private const uint EnumFlagGenericsMaskNonGeneric = 0x00000000; // no instantiation
        private const uint EnumFlagGenericsMaskGenericInst = 0x00000010; // regular instantiation, e.g. List<String>
        private const uint EnumFlagGenericsMaskSharedInst = 0x00000020; // shared instantiation, e.g., List<__Canon> or List<MyValueType<__Canon>>
        private const uint EnumFlagGenericsMaskTypicalInst = 0x00000030; // the type instantiated at its formal parameters, e.g., List<T>
        private const uint EnumFlagHasDefaultCtor = 0x00000200;
        private const uint EnumFlagIsByRefLike = 0x00001000;

        // WFLAGS_HIGH_ENUM
        private const uint EnumFlagContainsPointers = 0x01000000;
        private const uint EnumFlagHasComponentSize = 0x80000000;
        private const uint EnumFlagHasTypeEquivalence = 0x02000000;
        private const uint EnumFlagCategoryMask = 0x000F0000;
        private const uint EnumFlagCategoryValueType = 0x00040000;
        private const uint EnumFlagCategoryNullable = 0x00050000;
        private const uint EnumFlagCategoryPrimitiveValueType = 0x00060000; // subcategory of ValueType, Enum or primitive value type
        private const uint EnumFlagCategoryTruePrimitive = 0x00070000; // subcategory of ValueType, Primitive (ELEMENT_TYPE_I, etc.)
        private const uint EnumFlagCategoryValueTypeMask = 0x000C0000;
        private const uint EnumFlagCategoryInterface = 0x000C0000;
        // Types that require non-trivial interface cast have this bit set in the category
        private const uint EnumFlagNonTrivialInterfaceCast = 0x00080000 // enum_flag_Category_Array
                                                           | 0x40000000 // enum_flag_ComObject
                                                           | 0x00400000 // enum_flag_ICastable;
                                                           | 0x10000000 // enum_flag_IDynamicInterfaceCastable;
                                                           | 0x00040000; // enum_flag_Category_ValueType

        private const int DebugClassNamePtr = // adjust for debug_m_szClassName
#if DEBUG
#if UNITY_64
            8
#else
            4
#endif
#else
            0
#endif
            ;

        private const int ParentMethodTableOffset = 0x10 + DebugClassNamePtr;

#if UNITY_64
        private const int ElementTypeOffset = 0x30 + DebugClassNamePtr;
#else
        private const int ElementTypeOffset = 0x20 + DebugClassNamePtr;
#endif

#if UNITY_64
        private const int InterfaceMapOffset = 0x38 + DebugClassNamePtr;
#else
        private const int InterfaceMapOffset = 0x24 + DebugClassNamePtr;
#endif

        public bool ContainsGCPointers => (Flags & EnumFlagContainsPointers) != 0;
        public bool NonTrivialInterfaceCast => (Flags & EnumFlagNonTrivialInterfaceCast) != 0;
        public bool HasTypeEquivalence => (Flags & EnumFlagHasTypeEquivalence) != 0;
        public bool HasDefaultConstructor => (Flags & (EnumFlagHasComponentSize | EnumFlagHasDefaultCtor)) == EnumFlagHasDefaultCtor;

        private bool HasComponentSize => (Flags & EnumFlagHasComponentSize) != 0;

        public bool IsMultiDimensionalArray
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(HasComponentSize);
                // See comment on RawArrayData for details
                return BaseSize > (uint)(3 * sizeof(IntPtr));
            }
        }

        // Returns rank of multidimensional array rank, 0 for sz arrays
        public int MultiDimensionalArrayRank
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                Debug.Assert(HasComponentSize);
                // See comment on RawArrayData for details
                return (int)((BaseSize - (uint)(3u * sizeof(IntPtr))) / (2u * sizeof(int)));
            }
        }

        public bool IsInterface => (Flags & EnumFlagCategoryMask) == EnumFlagCategoryInterface;

        public bool IsValueType => (Flags & EnumFlagCategoryValueTypeMask) == EnumFlagCategoryValueType;

        public bool IsNullable => (Flags & EnumFlagCategoryMask) == EnumFlagCategoryNullable;

        public bool IsByRefLike => (Flags & (EnumFlagHasComponentSize | EnumFlagIsByRefLike)) == EnumFlagIsByRefLike;

        // Warning! UNLIKE the similarly named Reflection api, this method also returns "true" for Enums.
        public bool IsPrimitive => (Flags & EnumFlagCategoryMask) is EnumFlagCategoryPrimitiveValueType or EnumFlagCategoryTruePrimitive;

        public bool HasInstantiation => (Flags & EnumFlagHasComponentSize) == 0 && (Flags & EnumFlagGenericsMask) != EnumFlagGenericsMaskNonGeneric;

        public bool IsGenericTypeDefinition => (Flags & (EnumFlagHasComponentSize | EnumFlagGenericsMask)) == EnumFlagGenericsMaskTypicalInst;

        public bool IsConstructedGenericType
        {
            get
            {
                uint genericsFlags = Flags & (EnumFlagHasComponentSize | EnumFlagGenericsMask);
                return genericsFlags == EnumFlagGenericsMaskGenericInst || genericsFlags == EnumFlagGenericsMaskSharedInst;
            }
        }

        internal static bool AreSameType(MethodTable* mt1, MethodTable* mt2) => mt1 == mt2;

        /// <summary>
        /// Gets a <see cref="TypeHandle"/> for the element type of the current type.
        /// </summary>
        /// <remarks>This method should only be called when the current <see cref="MethodTable"/> instance represents an array or string type.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypeHandle GetArrayElementTypeHandle()
        {
            Debug.Assert(HasComponentSize);

            return new(ElementType);
        }

        [MethodImpl(MethodImplOptions.InternalCall)]
        public extern uint GetNumInstanceFieldBytes();
    }

    /// <summary>
    /// A type handle, which can wrap either a pointer to a <c>TypeDesc</c> or to a <see cref="MethodTable"/>.
    /// </summary>
    internal readonly unsafe struct TypeHandle
    {
        // Subset of src\vm\typehandle.h

        /// <summary>
        /// The address of the current type handles an object.
        /// </summary>
        private readonly void* _mAsTAddr;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TypeHandle(void* tAddr)
        {
            _mAsTAddr = tAddr;
        }

        /// <summary>
        /// Gets whether the current instance wraps a <see langword="null"/> pointer.
        /// </summary>
        public bool IsNull => _mAsTAddr is null;

        /// <summary>
        /// Gets whether this <see cref="TypeHandle"/> wraps a <c>TypeDesc</c> pointer.
        /// Only if this returns <see langword="false"/> it is safe to call <see cref="AsMethodTable"/>.
        /// </summary>
        private bool IsTypeDesc
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ((nint)_mAsTAddr & 2) != 0;
        }

        /// <summary>
        /// Gets the <see cref="MethodTable"/> pointer wrapped by the current instance.
        /// </summary>
        /// <remarks>This is only safe to call if <see cref="IsTypeDesc"/> returned <see langword="false"/>.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public MethodTable* AsMethodTable()
        {
            Debug.Assert(!IsTypeDesc);

            return (MethodTable*)_mAsTAddr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static TypeHandle TypeHandleOf<T>()
        {
            // return new TypeHandle((void*)RuntimeTypeHandle.ToIntPtr(typeof(T).TypeHandle));
            // RuntimeTypeHandle.ToIntPtr(value) => value.Value
            return new TypeHandle((void*)typeof(T).TypeHandle.Value);
        }
    }
}

#endif
