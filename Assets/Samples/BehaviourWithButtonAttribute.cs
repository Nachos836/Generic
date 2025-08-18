using System.Diagnostics;
using InspectorAttributes;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.UIElements;

namespace Generic.Samples
{
    [ApplyCustomUIProcessing]
    internal sealed partial class BehaviourWithButtonAttribute : MonoBehaviour
    {
        [Button(nameof(Test)), UsedImplicitly, Conditional("UNITY_EDITOR")]
        private void Test()
        {
            UnityEngine.Debug.Log("Test");
        }

        [Button(nameof(Test2)), UsedImplicitly, Conditional("UNITY_EDITOR")]
        private void Test2()
        {
            UnityEngine.Debug.Log("Test 2");
        }

        partial class Drawer
        {
            [Conditional("UNITY_EDITOR")]
            private partial void ApplyCustomUIProcessing(ref VisualElement defaultRoot, ref VisualElement customRoot)
            {
                defaultRoot.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
            }
        }
    }
}
