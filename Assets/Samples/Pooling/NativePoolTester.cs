using InspectorAttributes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Generic.Samples.Pooling
{
    internal sealed class NativePoolTester : MonoBehaviour
    {
        [SerializeField, Required] private PanelRenderer _renderer = default!;
        [SerializeField, Required] private PoolRoot _pool = default!;

        private int _version;

        private void OnEnable()
        {
            _renderer.RegisterUIReloadCallback(OnUIReloaded);
        }

        private void OnDisable()
        {
            _renderer.UnregisterUIReloadCallback(OnUIReloaded);
            _version = 0;
        }

        private void OnUIReloaded(PanelRenderer _, VisualElement root, int version)
        {
            if (version == _version) return;

            var warmup = root.Q<Button>(name: "WarmupButton").clickable;
            warmup.clicked -= OnWarmupClicked;
            warmup.clicked += OnWarmupClicked;

            var get = root.Q<Button>(name: "GetButton").clickable;
            get.clicked -= OnGetClicked;
            get.clicked += OnGetClicked;
        }

        private void OnGetClicked() => _pool.Spawn();
        private void OnWarmupClicked() => _pool.WarmUp();
    }
}
