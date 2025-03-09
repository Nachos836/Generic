using JetBrains.Annotations;
using UnityEngine;

namespace Generic.Samples
{
    using SerializableValueObjects;

    internal sealed class BehaviourWithDictionary : MonoBehaviour
    {
        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<string, string> _dictionary = SerializableDictionary<string, string>.Empty();

        [ContextMenu(nameof(Test))]
        private void Test()
        {
            UnityEngine.Debug.Log(_dictionary.ContainsKey("1"));
        }
    }
}
