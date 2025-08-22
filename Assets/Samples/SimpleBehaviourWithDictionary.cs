using JetBrains.Annotations;
using SerializableValueObjects;
using UnityEngine;

namespace Generic.Samples
{
    internal sealed class SimpleBehaviourWithDictionary : MonoBehaviour
    {
        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<int, int> _dictionary = SerializableDictionary.Empty<int, int>();
    }
}