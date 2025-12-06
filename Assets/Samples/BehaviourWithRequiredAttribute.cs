using InspectorAttributes;
using UnityEngine;

namespace Generic.Samples
{
    internal sealed class BehaviourWithRequiredAttribute : MonoBehaviour
    {
        [Required] [SerializeField] private Transform _target = default!;

        private void OnValidate()
        {
            _ = _target;
        }
    }
}
