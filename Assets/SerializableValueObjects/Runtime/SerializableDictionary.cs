#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;

using static System.Runtime.CompilerServices.MethodImplOptions;

namespace SerializableValueObjects
{
    public static partial class SerializableDictionary
    {
        public static SerializableDictionary<TKey, TValue> Empty<TKey, TValue>()
            where TKey : notnull => SerializableDictionary<TKey, TValue>.Empty();

        public static SerializableDictionary<TKey, TValue> Create<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> entries)
            where TKey : notnull => SerializableDictionary<TKey, TValue>.Create(entries);
    }

    [Serializable]
    public partial struct SerializableDictionary<TKey, TValue> where TKey : notnull
    {
        [SerializeField] private List<Entry> _entries;

        private FrozenDictionary<TKey, TValue> _backingDictionary;

        public readonly int Count => _backingDictionary.Count;
        public readonly ImmutableArray<TKey> Keys => _backingDictionary.Keys;
        public readonly ImmutableArray<TValue> Values => _backingDictionary.Values;

        public static SerializableDictionary<TKey, TValue> Empty() => new ()
        {
            _entries = new (),
            _backingDictionary = FrozenDictionary<TKey, TValue>.Empty
        };

        public static SerializableDictionary<TKey, TValue> Create(IEnumerable<KeyValuePair<TKey, TValue>> entries)
        {
            var candidates = entries.ToFrozenDictionary();

            return new SerializableDictionary<TKey, TValue>
            {
                _entries = candidates.Select(static keyValue => (Entry) keyValue).ToList(),
                _backingDictionary = candidates
            };
        }

        public static SerializableDictionary<TKey, TValue> Create(IReadOnlyCollection<KeyValuePair<TKey, TValue>> entries)
        {
            var candidates = entries.ToFrozenDictionary();

            return new SerializableDictionary<TKey, TValue>
            {
                _entries = candidates.Select(static keyValue => (Entry) keyValue).ToList(),
                _backingDictionary = candidates
            };
        }

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public readonly bool ContainsKey(TKey key) => _backingDictionary.ContainsKey(key);

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract")]
        public readonly bool TryGetValue(TKey key, [NotNullWhen(returnValue: true)] out TValue? value)
        {
            if (_backingDictionary.TryGetValue(key, out value))
            {
                return value is not null;
            }

            value = default;
            return false;
        }

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public readonly FrozenDictionary<TKey, TValue> AsFrozenDictionary() => _backingDictionary;

        public readonly ref readonly TValue this[TKey key]
        {
            [MustUseReturnValue] [MethodImpl(AggressiveInlining)] get => ref _backingDictionary[key];
        }

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public readonly FrozenDictionary<TKey, TValue>.Enumerator GetEnumerator() => _backingDictionary.GetEnumerator();

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public static implicit operator FrozenDictionary<TKey, TValue>(SerializableDictionary<TKey, TValue> source) => source._backingDictionary;

        [Serializable]
        internal partial struct Entry
        {
            [SerializeField] internal TKey _key;
            [SerializeField] internal TValue _value;

            internal Entry(TKey key, TValue value)
            {
                _key = key;
                _value = value;
            }

            public static implicit operator KeyValuePair<TKey, TValue>(Entry entry) => new (entry._key, entry._value);
            public static implicit operator Entry(KeyValuePair<TKey, TValue> entry) => new (entry.Key, entry.Value);
        }
    }
}
