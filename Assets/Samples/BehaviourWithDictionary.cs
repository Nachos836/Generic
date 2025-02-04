using Generic.SerializableValueObjects;
using JetBrains.Annotations;
using UnityEngine;

namespace Generic.Samples
{
    internal sealed class BehaviourWithDictionary : MonoBehaviour
    {
        [UsedImplicitly]
        [SerializeField] private SerializableDictionary<int, string> _dictionary = SerializableDictionary<int, string>.Create();
    }
}
