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

namespace Generic.SerializableValueObjects
{
    [Serializable]
    public struct SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver where TKey : notnull
    {
        [SerializeField] internal List<Entry> _entries;

        private FrozenDictionary<TKey, TValue> _backingDictionary;
        private List<KeyValuePair<TKey, TValue>> _orderedEntries;

        public readonly int Count => _backingDictionary.Count;
        public readonly ImmutableArray<TKey> Keys => _backingDictionary.Keys;
        public readonly ImmutableArray<TValue> Values => _backingDictionary.Values;

        public static SerializableDictionary<TKey, TValue> Empty() => new ()
        {
            _entries = new (),
            _backingDictionary = FrozenDictionary<TKey, TValue>.Empty,
            _orderedEntries = new ()
        };

        public static SerializableDictionary<TKey, TValue> Create(IEnumerable<KeyValuePair<TKey, TValue>> entries)
        {
            var candidates = entries.ToFrozenDictionary();

            return new SerializableDictionary<TKey, TValue>
            {
                _entries = candidates.Select(static keyValue => (Entry) keyValue).ToList(),
                _backingDictionary = candidates,
                _orderedEntries = new (candidates.Count)
            };
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
        #if UNITY_EDITOR
            var duplicates = _entries
                .Select(static (entry, index) => (index, entry))
                .Where(static record => record.entry._duplicated)
                .ToArray();
        #endif

            _entries.Clear();
            _entries.AddRange(_orderedEntries.Select(static value => (Entry) value));

        #if UNITY_EDITOR
            foreach (var (index, entry) in duplicates)
            {
                _entries.Insert(index, entry);
            }
        #endif
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _orderedEntries.Clear();
            for (var index = 0; index < _entries.Count; index++)
            {
                var entry = _entries[index];
                var key = entry._key;
                var canAddKey = _orderedEntries.Any(candidate => candidate.Key.Equals(key)) is false;
                if (canAddKey)
                {
                    _orderedEntries.Add(entry);
                }

                entry._duplicated = !canAddKey;
                _entries[index] = entry;
            }

            _backingDictionary = _orderedEntries.ToFrozenDictionary();

        #if !UNITY_EDITOR
            _entries.Clear();
        #endif
        }

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public readonly bool ContainsKey(TKey key) => _backingDictionary.ContainsKey(key);

        [MustUseReturnValue] [MethodImpl(AggressiveInlining)]
        public readonly bool TryGetValue(TKey key, [NotNullWhen(returnValue: true)] out TValue? value)
        {
            if (_backingDictionary.TryGetValue(key, out value!))
            {
                return true;
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
        internal struct Entry
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
        }
    }
}
