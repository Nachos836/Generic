#nullable enable

using UnityEngine;

// ReSharper disable once CheckNamespace
namespace SerializableValueObjects
{
    partial struct SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver where TKey : notnull
    {
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        #if UNITY_EDITOR

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _backingDictionary = null;
        }

        #else

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            if (_entries.Count == 0)
            {
                _backingDictionary = System.Collections.Frozen.FrozenDictionary<TKey, TValue>.Empty;
            }
            else
            {
                _backingDictionary = System.Collections.Frozen.FrozenDictionary.ToFrozenDictionary
                (
                    _entries,
                    keySelector: static entry => entry._key,
                    elementSelector: static entry => entry._value
                );
            }
        }

        #endif
    }
}
