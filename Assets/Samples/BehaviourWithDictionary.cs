using SerializableValueObjects;
using System;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;

using static SerializableValueObjects.SerializableDictionary;

namespace Generic.Samples
{
    internal sealed class BehaviourWithDictionary : MonoBehaviour
    {
        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<string, string> _dictionary = Empty<string, string>();

        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<GameObject, UnityEvent> _anotherDictionary = Empty<GameObject, UnityEvent>();

        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<string, UnityEvent> _yetAnotherDictionary = Empty<string, UnityEvent>();

        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<int, Custom> _customDictionary = Empty<int, Custom>();

        [ContextMenu(nameof(Test))]
        private void Test()
        {
            UnityEngine.Debug.Log(_dictionary.ContainsKey("1"));
        }

        [Serializable]
        private struct Custom
        {
            [SerializeField] [UsedImplicitly] private int _a;
            [SerializeField] [UsedImplicitly] private int _b;

            [UsedImplicitly]
            private void Dummy()
            {
                _a = 24;
                _b = 42;

                _ = _a;
                _ = _b;
            }
        }
    }
}
