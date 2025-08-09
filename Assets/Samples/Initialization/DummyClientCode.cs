using Generic.Samples.Initialization.Services;
using UnityEngine;

namespace Generic.Samples.Initialization
{
    public sealed class DummyClientCode : MonoBehaviour
    {
        [SerializeField] private DummyService _service = default!;

        private void OnEnable()
        {
            _service.RunMe();
        }
    }
}
