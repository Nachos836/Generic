#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.Pool;
using UnityEngine.UIElements;

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

            var horizontalContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginLeft = 3,
                }
            };

            var label = new Label(property.displayName + " Selector")
            {
                style =
                {
                    minWidth = 120,
                    marginRight = 4
                }
            };

            var dropdown = new DropdownField
            {
                style =
                {
                    flexGrow = 1
                }
            };

            var propertyField = new PropertyField(property)
            {
                style =
                {
                    marginTop = 5
                }
            };
            propertyField.BindProperty(property);

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

            dropdown.RegisterValueChangedCallback(@event =>
            {
                if (@event.newValue == "None")
                {
                    property.managedReferenceValue = null;
                }
                else
                {
                    var selectedType = Array.Find(implementations, type => type.Name == @event.newValue);
                    if (selectedType != null)
                    {
                        property.managedReferenceValue = Activator.CreateInstance(selectedType);
                    }
                }
                property.serializedObject.ApplyModifiedProperties();
            });

            return container;
        }

        private static Type? GetFieldType(SerializedProperty property)
        {
            var candidate = property.serializedObject.targetObject.GetType()
                .GetField(property.propertyPath,
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

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
                    types.Sort(comparer: ByNames.ComparerInstance);
                    goto case 1;
                case 1:
                    result = types.ToArray();
                    break;
            }

            CachedImplementations[baseType] = result;
            return result;
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
