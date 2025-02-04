#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEngine;

namespace Generic.SerializableValueObjects
{
    [Serializable]
    public struct SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver where TKey : notnull
    {
        [SerializeField] internal List<Entry> _entries;

        private FrozenDictionary<TKey, TValue> _dictionary;

        public readonly int Count => _dictionary.Count;
        public readonly ImmutableArray<TKey> Keys => _dictionary.Keys;
        public readonly ImmutableArray<TValue> Values => _dictionary.Values;

        public static SerializableDictionary<TKey, TValue> Create() => new()
        {
            _entries = new (),
            _dictionary = FrozenDictionary<TKey, TValue>.Empty
        };

        public static SerializableDictionary<TKey, TValue> Create(IReadOnlyCollection<KeyValuePair<TKey, TValue>> entries) => new()
        {
            _entries = entries.Select(static keyValue => (Entry)keyValue)
                .ToList(),
            _dictionary = new Dictionary<TKey, TValue>(entries)
                .ToFrozenDictionary()
        };

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            _entries.Clear();

            foreach (var (key, value) in this)
            {
                _entries.Add(new Entry(key, value));
            }
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _dictionary = new Dictionary<TKey, TValue>(_entries.Select(static entry => entry.AsKeyValuePair()))
                .ToFrozenDictionary();
        }

        public readonly bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public readonly bool TryGetValue(TKey key, [NotNullWhen(returnValue: true)] out TValue? value)
        {
            if (_dictionary.TryGetValue(key, out value!))
            {
                return true;
            }

            value = default;
            return false;
        }

        public readonly ref readonly TValue this[TKey key] => ref _dictionary[key];
        public readonly FrozenDictionary<TKey, TValue>.Enumerator GetEnumerator() => _dictionary.GetEnumerator();

        internal IEnumerable<int> DuplicateKeysIndexes()
        {
            for (var current = 0; current < _entries.Count; current++)
            {
                var entry = _entries[current];
                if (_entries.Skip(current + 1).Any(other => entry.SimilarAs(other)))
                {
                    yield return current;
                }
            }
        }

        private bool ContainsDuplicatedKeys() => DuplicateKeysIndexes().Any();

        [Serializable]
        public struct Entry
        {
            [SerializeField] internal TKey _key;
            [SerializeField] internal TValue _value;

            internal Entry(TKey key, TValue value) => (_key, _value) = (key, value);

            internal bool SimilarAs(Entry other) => _key.Equals(other._key);

            public static implicit operator KeyValuePair<TKey, TValue>(Entry entry) => new (entry._key, entry._value);
            public static implicit operator Entry(KeyValuePair<TKey, TValue> entry) => new (entry.Key, entry.Value);

            public KeyValuePair<TKey, TValue> AsKeyValuePair() => this;
        }
    }
}
