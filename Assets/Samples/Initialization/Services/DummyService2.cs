using Initializer;
using JetBrains.Annotations;
using UnityEngine;

namespace Generic.Samples.Initialization.Services
{
    public sealed class DummyService2 : ServiceAsset, IInitializable
    {
        [SerializeField] [UsedImplicitly] private DummyService _service = default!;

        void IInitializable.Initialize()
        {
            _ = _service;

            UnityEngine.Debug.Log("Whoa! 2");
        }
    }
}
