using SerializableValueObjects;
using JetBrains.Annotations;
using UnityEngine;

namespace Generic.Samples
{
    internal sealed class BehaviourWithTimeSpan : MonoBehaviour
    {
        [UsedImplicitly]
        [SerializeField] private SerializableTimeSpan _timeSpan = SerializableTimeSpan.Zero;
    }
}
