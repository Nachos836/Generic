#if UNITY_EDITOR

#nullable enable

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using dotNetCompat.Extensions;
using UnityEngine;

// ReSharper disable once CheckNamespace
namespace SerializableValueObjects
{
    using DrawerDictionary = SerializableDictionary<int, int>;

    static partial class SerializableDictionary
    {
        internal const string ListName = DrawerDictionary.ListName;
        internal const string KeyName = DrawerDictionary.Entry.KeyName;
        internal const string ValueName = DrawerDictionary.Entry.ValueName;
        internal const string StagingName = DrawerDictionary.StagingName;
    }

    internal interface ISerializableDictionaryRawAccess
    {
        IList BackingCollection { get; }
        bool CheckUpdatedKey(int index, object rawKey);
    }

    [StructLayout(LayoutKind.Auto)]
    partial struct SerializableDictionary<TKey, TValue> : ISerializableDictionaryRawAccess
    {
        internal const string ListName = nameof(_entries);
        internal const string StagingName = nameof(_staging);

        [SerializeField] private Entry[] _staging;

        IList ISerializableDictionaryRawAccess.BackingCollection => _entries;

        bool ISerializableDictionaryRawAccess.CheckUpdatedKey(int index, object rawKey)
        {
            var builder = ImmutableDictionary.CreateBuilder<TKey, TValue>();
            var current = 0;
            foreach (ref readonly var entry in _entries.AsReadOnlySpan())
            {
                if (current == index)
                {
                    if (Entry.TryCastKey(rawKey, out var checkKey))
                    {
                        if (builder.TryAdd(checkKey, default!) is false) return false;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    if (entry is { _key: not null })
                    {
                        if (builder.TryAdd(entry._key, default!) is false) return false;
                    }
                    else
                    {
                        return false;
                    }
                }

                ++current;
            }

            return true;
        }

        internal partial struct Entry
        {
            internal const string KeyName = nameof(_key);
            internal const string ValueName = nameof(_value);

            internal static bool TryCastKey(object key, out TKey cast)
            {
                if (key is TKey candidate)
                {
                    cast = candidate;
                    return true;
                }
                else
                {
                    cast = default!;
                    return false;
                }
            }
        }
    }
}

#endif
