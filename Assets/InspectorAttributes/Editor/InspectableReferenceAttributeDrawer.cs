#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

using static System.Reflection.BindingFlags;

namespace InspectorAttributes.Editor
{
    [CustomPropertyDrawer(typeof(InspectableReferenceAttribute))]
    internal sealed class InspectableReferenceAttributeDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, Type[]> CachedImplementations = new ();
        private static readonly Dictionary<string, Type?> CachedTypes = new ();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.EnableInClassList("unity-property-field", enable: true);
            container.EnableInClassList("unity-property-field__inspector-property", enable: true);

            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
                container.Add(new HelpBox
                (
                    text: "InspectableReference attribute can only be used with [SerializeReference] fields",
                    HelpBoxMessageType.Error
                ));
                return container;
            }

            var fieldType = GetFieldType(property);
            if (fieldType is null or { IsInterface: false, IsAbstract: false })
            {
                container.Add(new HelpBox
                (
                    text: "InspectableReference attribute should be used with interface or abstract class types",
                    HelpBoxMessageType.Warning
                ));
                return container;
            }

            var horizontalContainer = new VisualElement();
            horizontalContainer.EnableInClassList("unity-base-popup-field", enable: true);
            horizontalContainer.EnableInClassList("unity-popup-field", enable: true);
            horizontalContainer.EnableInClassList("unity-base-field", enable: true);
            horizontalContainer.EnableInClassList("unity-base-field__aligned", enable: true);
            horizontalContainer.EnableInClassList("unity-base-field__inspector-field", enable: true);

            var label = new Label(property.displayName + " Selector");
            label.EnableInClassList("unity-label", enable: true);
            label.EnableInClassList("unity-text-element", enable: true);
            label.EnableInClassList("unity-base-field__label", enable: true);
            label.EnableInClassList("unity-base-popup-field__label", enable: true);
            label.EnableInClassList("unity-popup-field__label", enable: true);
            label.EnableInClassList("unity-property-field__label", enable: true);

            var dropdown = new DropdownField
            {
                style = { marginLeft = 0 }
            };
            dropdown.EnableInClassList("unity-base-field__input", enable: true);
            dropdown.EnableInClassList("unity-popup-field__input", enable: true);
            dropdown.EnableInClassList("unity-property-field__input", enable: true);

            var propertyField = new PropertyField(property, property.displayName + " Editor");
            propertyField.BindProperty(property);
            propertyField.EnableInClassList("unity-property-field", enable: true);
            propertyField.EnableInClassList("unity-property-field__inspector-property", enable: true);

            horizontalContainer.Add(label);
            horizontalContainer.Add(dropdown);
            container.Add(horizontalContainer);
            container.Add(propertyField);

            var choices = new List<string> { "None" };
            var implementations = GetImplementationsCached(fieldType);
            choices.AddRange(implementations.Select(static type => type.Name));

            dropdown.choices = choices;

            var currentType = GetCurrentType(property);
            dropdown.value = currentType != null ? currentType.Name : "None";

            UpdatePropertyFieldVisibility(property, propertyField);
            dropdown.RegisterValueChangedCallback(@event =>
            {
                if (@event.newValue == "None")
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    if (Array.Find(implementations, type => type.Name == @event.newValue) is { } selectedType)
                    {
                        property.managedReferenceValue = Activator.CreateInstance(selectedType);
                    }
                }
                property.serializedObject.ApplyModifiedProperties();
                UpdatePropertyFieldVisibility(property, propertyField);
            });

            container.RegisterCallback<GeometryChangedEvent, RootAndLabelToResize>
            (
                static (_, resizer) => resizer.SetDesiredSize(),
                new RootAndLabelToResize(container, label)
            );

            return container;
        }

        private static Type? GetFieldType(SerializedProperty property)
        {
            var candidate = property.serializedObject.targetObject.GetType()
                .GetField(property.propertyPath, bindingAttr : Public | NonPublic | Instance);

            return candidate?.FieldType;
        }

        private static Type? GetCurrentType(SerializedProperty property)
        {
            var typeString = property.managedReferenceFullTypename;
            if (string.IsNullOrEmpty(typeString)) return null;

            if (CachedTypes.TryGetValue(typeString, out var cachedType)) return cachedType;

            var typeParts = typeString.Split(' ');
            if (typeParts.Length != 2)
            {
                CachedTypes[typeString] = null;
                return null;
            }

            var assemblyName = typeParts[0];
            var typeName = typeParts[1];

            // This is faster than Search within Assembly
            var assemblyQualifiedName = string.Format("{0}, {1}", typeName, assemblyName);
            var type = Type.GetType(assemblyQualifiedName, throwOnError: false);

            CachedTypes[typeString] = type;

            return type;
        }

        private static Type[] GetImplementationsCached(Type baseType)
        {
            if (CachedImplementations.TryGetValue(baseType, out var cached)) return cached;

            using var handler = ListPool<Type>.Get(out var types);

            if (baseType.IsInterface)
            {
                types.AddRange(TypeCache.GetTypesDerivedFrom(baseType).Where(type => type is
                {
                    IsAbstract: false,
                    IsInterface: false,
                    IsGenericTypeDefinition: false
                }));
            }
            else if (baseType.IsAbstract)
            {
                types.AddRange(TypeCache.GetTypesDerivedFrom(baseType).Where(type => type is
                {
                    IsAbstract: false,
                    IsGenericTypeDefinition: false
                }));
            }

            var result = Array.Empty<Type>();
            switch (types.Count)
            {
                case > 1:
                    types.Sort(ByNames.ComparerInstance);
                    goto case 1;
                case 1:
                    result = types.ToArray();
                    goto default;
                default:
                    CachedImplementations[baseType] = result;
                    return result;
            }
        }

        private static void UpdatePropertyFieldVisibility(SerializedProperty property, VisualElement propertyField)
        {
            propertyField.style.display = HasVisibleChildren(property)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private static bool HasVisibleChildren(SerializedProperty property)
        {
            if (property.managedReferenceValue == null) return false;

            var copy = property.Copy();

            return copy.NextVisible(enterChildren: true)
                && IsChildOf(copy, parent: property);
        }

        private static bool IsChildOf(SerializedProperty child, SerializedProperty parent)
        {
            return child.propertyPath.StartsWith(parent.propertyPath + ".");
        }

        private readonly struct RootAndLabelToResize
        {
            private readonly VisualElement _root;
            private readonly VisualElement _label;

            public RootAndLabelToResize(VisualElement root, VisualElement label)
            {
                _root = root;
                _label = label;
            }

            public void SetDesiredSize()
            {
                var width = _root.panel.visualTree.resolvedStyle.width;

                _label.style.width = Mathf.Max(Mathf.Ceil(width * 0.45f) - 40f, 120f);
            }
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            CachedImplementations.Clear();
            CachedTypes.Clear();
        }

        private sealed class ByNames : IComparer<Type>
        {
            public static IComparer<Type> ComparerInstance { get; } = new ByNames();

            int IComparer<Type>.Compare(Type? first, Type? second)
            {
                if (ReferenceEquals(first, second)) return 0;
                if (second is null) return 1;
                if (first is null) return -1;
                return string.Compare(first.Name, second.Name, StringComparison.Ordinal);
            }
        }
    }
}
