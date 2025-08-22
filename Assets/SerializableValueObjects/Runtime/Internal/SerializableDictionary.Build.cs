#if !UNITY_EDITOR

#nullable enable

using System.Collections.Frozen;
using UnityEngine;

namespace SerializableValueObjects
{
    partial struct SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver where TKey : notnull
    {
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            _entries ??= new ();
            if (_entries.Count == 0)
            {
                _backingDictionary = FrozenDictionary<TKey, TValue>.Empty;
                return;
            }

            _backingDictionary = _entries.ToFrozenDictionary
            (
                keySelector: static entry => entry._key,
                elementSelector:  static entry => entry._value
            );
        }
    }
}

#endif
