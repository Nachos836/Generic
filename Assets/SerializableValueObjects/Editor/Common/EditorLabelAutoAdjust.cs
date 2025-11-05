using UnityEngine;
using UnityEngine.UIElements;

namespace SerializableValueObjects.Editor.Common
{
    internal readonly struct EditorLabelAutoAdjust
    {
        private readonly VisualElement _root;
        private readonly VisualElement _label;

        public EditorLabelAutoAdjust(VisualElement root, VisualElement label)
        {
            _root = root;
            _label = label;
        }

        public void Adjust()
        {
            var width = _root.panel.visualTree.resolvedStyle.width;

            _label.style.width = Mathf.Max(Mathf.Ceil(width * 0.45f) - 40f, 120f);
        }
    }
}
