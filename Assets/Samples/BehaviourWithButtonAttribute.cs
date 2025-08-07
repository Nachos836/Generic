using System.Diagnostics;
using InspectorAttributes;
using JetBrains.Annotations;
using UnityEngine;

namespace Generic.Samples
{
    internal sealed partial class BehaviourWithButtonAttribute : MonoBehaviour
    {
        [Button(nameof(Test)), UsedImplicitly, Conditional("UNITY_EDITOR")]
        private void Test()
        {
            UnityEngine.Debug.Log("Test");
        }
    }
}
