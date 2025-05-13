using System;
using System.Linq;
using UnityEditor.Search;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

using static System.Reflection.BindingFlags;

namespace InspectorAttributes.Editor
{
    // ToDo: Enable When Codegen Will Avaliable
    // [CustomEditor(typeof(MonoBehaviour), editorForChildClasses: true)]
    // [CanEditMultipleObjects]
    internal sealed class ButtonAttributeDrawer : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var methods = target.GetType()
                .GetMethods(Instance | Static | Public | NonPublic)
                .Where(static method => Attribute.IsDefined(method, typeof(ButtonAttribute)))
                .Where(static method => method.GetParameters().Length is 0);

            foreach (var method in methods)
            {
                root.Add(new Button(() => method.Invoke(target, null))
                {
                    text = method.GetAttribute<ButtonAttribute>().Label,
                    style =
                    {
                        alignSelf = Align.Center,
                        justifyContent = Justify.Center,
                        flexGrow = 1,
                        minWidth = Length.Percent(20),
                        height = 22,
                        marginTop = 2,
                        marginBottom = 2,
                        marginLeft = 4,
                        marginRight = 4
                    }
                });
            }

            return root;
        }
    }
}
