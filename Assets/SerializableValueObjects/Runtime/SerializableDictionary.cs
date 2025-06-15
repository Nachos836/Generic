#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using UnityEngine;

using static System.Runtime.CompilerServices.MethodImplOptions;

namespace SerializableValueObjects
{
    public static class SerializableDictionary
    {
        public static SerializableDictionary<TKey, TValue> Empty<TKey, TValue>()
            where TKey : notnull => SerializableDictionary<TKey, TValue>.Empty();

        public static SerializableDictionary<TKey, TValue> Create<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> entries)
            where TKey : notnull => SerializableDictionary<TKey, TValue>.Create(entries);
    }

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
                _orderedEntries = candidates.ToList()
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
                var canAddKey = _orderedEntries.Contains(new KeyValuePair<TKey, TValue>(key, default!), KeyValueComparer.ByKey) is false;
                if (canAddKey)
                {
                    _orderedEntries.Add(entry);
                }

            #if UNITY_EDITOR
                entry._duplicated = !canAddKey;
                _entries[index] = entry;
            #endif
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

            public static implicit operator KeyValuePair<TKey, TValue>(Entry entry) => new (entry._key, entry._value);
            public static implicit operator Entry(KeyValuePair<TKey, TValue> entry) => new (entry.Key, entry.Value);
        }

        private static class KeyEqualityComparer
        {
            public static readonly Func<TKey, TKey, bool> Invoke = GetComparator();
            public new static readonly Func<TKey, int> GetHashCode = GetHasher();

            private static Func<TKey, TKey, bool> GetComparator()
            {
                var keyType = typeof(TKey);

                if (typeof(IEqualityComparer<TKey>).IsAssignableFrom(keyType)) return GenerateViaInterface<IEqualityComparer<TKey>>("Equals");
                if (typeof(IEquatable<TKey>).IsAssignableFrom(keyType)) return GenerateViaInterface<IEquatable<TKey>>("Equals");

                if (typeof(IComparable<TKey>).IsAssignableFrom(keyType))
                {
                    var first = Expression.Parameter(keyType, "a");
                    var second = Expression.Parameter(keyType, "b");
                    var call = Expression.Call
                    (
                        Expression.Convert(first, typeof(IComparable<TKey>)),
                        typeof(IComparable<TKey>).GetMethod(nameof(IComparable<TKey>.CompareTo))!,
                        second
                    );

                    var body = Expression.Equal(call, Expression.Constant(0));
                    return Expression.Lambda<Func<TKey, TKey, bool>>(body, first, second)
                        .Compile();
                }

                return EqualityComparer<TKey>.Default.Equals;

                static Func<TKey, TKey, bool> GenerateViaInterface<TInterface>(string methodName)
                {
                    var type = typeof(TKey);
                    var first = Expression.Parameter(type, "a");
                    var second = Expression.Parameter(type, "b");

                    var call = Expression.Call
                    (
                        Expression.Convert(first, typeof(TInterface)),
                        typeof(TInterface).GetMethod(methodName, new[] { type })!,
                        second
                    );

                    return Expression.Lambda<Func<TKey, TKey, bool>>(call, first, second)
                        .Compile();
                }
            }

            private static Func<TKey, int> GetHasher()
            {
                var type = typeof(TKey);

                if (typeof(IEqualityComparer<TKey>).IsAssignableFrom(type))
                {
                    var key = Expression.Parameter(type, "k");
                    var call = Expression.Call
                    (
                        Expression.Convert(key, typeof(IEqualityComparer<TKey>)),
                        typeof(IEqualityComparer<TKey>).GetMethod(nameof(IEqualityComparer<TKey>.GetHashCode))!,
                        key
                    );

                    return Expression.Lambda<Func<TKey, int>>(call, key)
                        .Compile();
                }
                else
                {
                    var key = Expression.Parameter(type, "k");
                    var call = Expression.Call(key, type.GetMethod(nameof(GetHashCode), Type.EmptyTypes)!);

                    return Expression.Lambda<Func<TKey, int>>(call, key)
                        .Compile();
                }
            }
        }

        private static class KeyValueComparer
        {
            public static readonly IEqualityComparer<KeyValuePair<TKey, TValue>> ByKey = new ComparerByKey();

            private sealed class ComparerByKey : IEqualityComparer<KeyValuePair<TKey, TValue>>
            {
                public bool Equals(KeyValuePair<TKey, TValue> first, KeyValuePair<TKey, TValue> second)
                {
                    return KeyEqualityComparer.Invoke(first.Key, second.Key);
                }

                public int GetHashCode(KeyValuePair<TKey, TValue> income)
                {
                    return KeyEqualityComparer.GetHashCode(income.Key);
                }
            }
        }


    }
}
