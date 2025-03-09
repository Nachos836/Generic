#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEngine;

using static System.Runtime.CompilerServices.MethodImplOptions;

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

        public static SerializableDictionary<TKey, TValue> Empty() => new ()
        {
            _entries = new (),
            _dictionary = FrozenDictionary<TKey, TValue>.Empty
        };

        public static SerializableDictionary<TKey, TValue> Create(IReadOnlyCollection<KeyValuePair<TKey, TValue>> entries) => new ()
        {
            _entries = entries.Select(static keyValue => (Entry) keyValue)
                .ToList(),
            _dictionary = new Dictionary<TKey, TValue>(entries)
                .ToFrozenDictionary()
        };

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        #if UNITY_EDITOR
            var duplicates = _entries
                .Select(static (entry, index) => (index, entry))
                .Where(static record => record.entry._duplicated)
                .ToArray();
        #endif

            _entries.Clear();
            _entries.AddRange(_dictionary.Select(static value => (Entry) value));

        #if UNITY_EDITOR
            foreach (var (index, entry) in duplicates)
            {
                _entries.Insert(index, entry);
            }
        #endif
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            var temp = new Dictionary<TKey, TValue>();
            for (var index = 0; index < _entries.Count; index++)
            {
                var entry = _entries[index];
                var key = entry._key;
                var canAddKey = temp.ContainsKey(key) is false;
                if (canAddKey)
                {
                    temp.Add(key, entry._value);
                }

                entry._duplicated = !canAddKey;
                _entries[index] = entry;
            }

            _dictionary = temp.ToFrozenDictionary();

        #if !UNITY_EDITOR
            _entries.Clear();
        #endif
        }

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public readonly bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
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

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public readonly FrozenDictionary<TKey, TValue>.Enumerator GetEnumerator() => _dictionary.GetEnumerator();

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public static implicit operator FrozenDictionary<TKey, TValue>(SerializableDictionary<TKey, TValue> source) => source._dictionary;

        [Serializable]
        [StructLayout(LayoutKind.Auto)]
        public struct Entry
        {
        #if UNITY_EDITOR
            [SerializeField] internal bool _duplicated;
        #endif
            [SerializeField] internal TKey _key;
            [SerializeField] internal TValue _value;

            internal Entry(TKey key, TValue value)
            {
                _key = key;
                _value = value;
        #if UNITY_EDITOR
                _duplicated = false;
        #endif
            }

            internal readonly bool SimilarAs(Entry other) => _key.Equals(other._key);

            public static implicit operator KeyValuePair<TKey, TValue>(Entry entry) => new (entry._key, entry._value);
            public static implicit operator Entry(KeyValuePair<TKey, TValue> entry) => new (entry.Key, entry.Value);

            public KeyValuePair<TKey, TValue> AsKeyValuePair() => this;
        }
    }
}
