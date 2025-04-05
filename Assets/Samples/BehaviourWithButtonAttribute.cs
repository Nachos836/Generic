using InspectorAttributes;
using JetBrains.Annotations;
using UnityEngine;

namespace Generic.Samples
{
    internal sealed class BehaviourWithButtonAttribute : MonoBehaviour
    {
        [Button(nameof(Test)), UsedImplicitly]
        private void Test()
        {
            UnityEngine.Debug.Log("Test");
        }
    }
}
