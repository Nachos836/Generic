using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;

namespace Generic.Samples
{
    using SerializableValueObjects;

    internal sealed class BehaviourWithDictionary : MonoBehaviour
    {
        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<string, string> _dictionary = SerializableDictionary<string, string>.Empty();

        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<GameObject, UnityEvent> _anotherDictionary = SerializableDictionary<GameObject, UnityEvent>.Empty();

        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<string, UnityEvent> _yetAnotherDictionary = SerializableDictionary<string, UnityEvent>.Empty();


        [ContextMenu(nameof(Test))]
        private void Test()
        {
            UnityEngine.Debug.Log(_dictionary.ContainsKey("1"));
        }
    }
}
