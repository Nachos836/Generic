#nullable enable

using System.Diagnostics;
using System.Runtime.CompilerServices;
using UnityEngine;

using static System.Runtime.CompilerServices.MethodImplOptions;

// ReSharper disable once CheckNamespace
namespace SerializableValueObjects
{
    partial struct SerializableDictionary<TKey, TValue> : ISerializationCallbackReceiver where TKey : notnull
    {
        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            ResetBackingDictionaryForEditor();
            PopulateBackingDictionaryForBuild();
        }

        [Conditional("UNITY_EDITOR")]
        [MethodImpl(AggressiveInlining)]
        private void ResetBackingDictionaryForEditor()
        {
            _backingDictionary = null;
        }

        [MethodImpl(AggressiveInlining)]
        private void PopulateBackingDictionaryForBuild()
        {
            #if UNITY_EDITOR
                if (Application.isEditor) return;
            #endif

            if (_entries is null or { Count: 0})
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
    }
}
