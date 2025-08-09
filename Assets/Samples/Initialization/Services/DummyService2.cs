using Initializer;

namespace Generic.Samples.Initialization.Services
{
    public sealed class DummyService2 : ServiceAsset, IInitializable
    {
        void IInitializable.Initialize()
        {
            UnityEngine.Debug.Log("Whoa! 2");
        }
    }
}
