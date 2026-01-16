#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
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

        private FrozenDictionary<TKey, TValue>? _backingDictionary;

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
        public FrozenDictionary<TKey, TValue> AsFrozenDictionary()
        {
            if (_backingDictionary is not null) return _backingDictionary;
            if (_entries.Count == 0) return FrozenDictionary<TKey, TValue>.Empty;

            return _backingDictionary = _entries.ToFrozenDictionary
            (
                keySelector: static entry => entry._key,
                elementSelector:  static entry => entry._value
            );
        }

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public static implicit operator FrozenDictionary<TKey, TValue>(SerializableDictionary<TKey, TValue> source)
        {
            return source.AsFrozenDictionary();
        }

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
