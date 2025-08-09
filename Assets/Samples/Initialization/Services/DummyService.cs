using Initializer;

namespace Generic.Samples.Initialization.Services
{
    public sealed class DummyService : ServiceAsset, IInitializable
    {
        void IInitializable.Initialize()
        {
            UnityEngine.Debug.Log("Whoa! 1");
        }

        public void RunMe() => UnityEngine.Debug.Log("Run Me!");
    }
}
