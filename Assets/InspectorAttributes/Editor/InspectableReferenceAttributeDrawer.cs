#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

using static System.Reflection.BindingFlags;

namespace InspectorAttributes.Editor
{
    using Common;

    [CustomPropertyDrawer(typeof(InspectableReferenceAttribute))]
    internal sealed class InspectableReferenceAttributeDrawer : PropertyDrawer
    {
        private static readonly Dictionary<Type, Type[]> CachedImplementations = new();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();
            root.EnableInClassList("unity-property-field", enable: true);
            root.EnableInClassList("unity-property-field__inspector-property", enable: true);

            var fieldType = GetFieldType(property);
            if (fieldType == null)
            {
                root.Add(new HelpBox("Unable to resolve field type for property path.", HelpBoxMessageType.Error));
                return root;
            }

            // Single item reference
            if (property.propertyType == SerializedPropertyType.ManagedReference)
            {
                if (!IsInspectableBaseType(fieldType))
                {
                    root.Add(new HelpBox(
                        text: "InspectableReference should target interface/abstract class or a collection of such element type.",
                        HelpBoxMessageType.Warning
                    ));
                    return root;
                }

                root.Add(CreateReferenceEditor(property, fieldType, property.displayName));
                return root;
            }

            // Collection of references (List<T>, T[])
            if (TryGetCollectionElementType(fieldType, out var elementType) && IsInspectableBaseType(elementType))
            {
                root.Add(CreateCollectionEditor(property, elementType, property.displayName));
                return root;
            }

            root.Add(new HelpBox(
                text: "InspectableReference can be used only with [SerializeReference] field or collection of interface/abstract element type.",
                HelpBoxMessageType.Error
            ));
            return root;
        }

        private static VisualElement CreateReferenceEditor(SerializedProperty property, Type baseType, string labelText)
        {
            var container = new VisualElement();

            var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            header.EnableInClassList("unity-base-popup-field", enable: true);
            header.EnableInClassList("unity-popup-field", enable: true);
            header.EnableInClassList("unity-base-field", enable: true);
            header.EnableInClassList("unity-base-field__aligned", enable: true);
            header.EnableInClassList("unity-base-field__inspector-field", enable: true);

            var label = new Label(labelText + " Selector");
            label.EnableInClassList("unity-label", enable: true);
            label.EnableInClassList("unity-text-element", enable: true);
            label.EnableInClassList("unity-base-field__label", enable: true);
            label.EnableInClassList("unity-base-popup-field__label", enable: true);
            label.EnableInClassList("unity-popup-field__label", enable: true);
            label.EnableInClassList("unity-property-field__label", enable: true);

            var dropdown = new DropdownField { style = { marginLeft = 0, flexGrow = 1 } };
            dropdown.EnableInClassList("unity-base-field__input", enable: true);
            dropdown.EnableInClassList("unity-popup-field__input", enable: true);
            dropdown.EnableInClassList("unity-property-field__input", enable: true);

            var propertyField = new PropertyField(property, labelText + " Editor");
            propertyField.BindProperty(property);
            propertyField.EnableInClassList("unity-property-field", enable: true);
            propertyField.EnableInClassList("unity-property-field__inspector-property", enable: true);

            header.Add(label);
            header.Add(dropdown);
            container.Add(header);
            container.Add(propertyField);

            var propertyPath = property.propertyPath;
            var isApplyingFromDropdown = false;

            RebuildDropdownAndValue(property);

            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (dropdown.userData is not ChoiceModel model) return;
                if (!model.Actions.TryGetValue(evt.newValue, out var action)) return;

                isApplyingFromDropdown = true;
                try
                {
                    action.Invoke();
                    property.serializedObject.ApplyModifiedProperties();
                    property.serializedObject.Update();
                }
                finally
                {
                    isApplyingFromDropdown = false;
                }

                var refreshed = property.serializedObject.FindProperty(propertyPath);
                if (refreshed != null)
                {
                    RebuildDropdownAndValue(refreshed);
                }
            });

            container.TrackSerializedObjectValue(property.serializedObject, _ =>
            {
                if (isApplyingFromDropdown) return;

                var refreshed = property.serializedObject.FindProperty(propertyPath);
                if (refreshed != null)
                {
                    RebuildDropdownAndValue(refreshed);
                }
            });

            container.RegisterCallback<GeometryChangedEvent, EditorLabelAutoAdjust>(
                static (_, resizer) => resizer.Adjust(),
                new EditorLabelAutoAdjust(container, label));

            return container;

            void RebuildDropdownAndValue(SerializedProperty currentProperty)
            {
                var model = BuildChoiceModel(currentProperty, baseType);
                dropdown.choices = model.Choices;
                dropdown.SetValueWithoutNotify(GetCurrentChoiceLabel(currentProperty, model));
                UpdatePropertyFieldVisibility(currentProperty, propertyField);
                dropdown.userData = model;
            }
        }

        private static VisualElement CreateCollectionEditor(SerializedProperty property, Type elementType, string labelText)
        {
            var captureProperty = property;
            var capturedElementType = elementType;

            var root = new VisualElement();
            var foldout = new Foldout { text = labelText, value = property.isExpanded };
            foldout.RegisterValueChangedCallback(evt =>
            {
                captureProperty.isExpanded = evt.newValue;
                captureProperty.serializedObject.ApplyModifiedProperties();
            });

            var content = new VisualElement { style = { marginLeft = 14 } };
            var toolbar = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var addButton = new Button(() =>
            {
                captureProperty.arraySize++;
                captureProperty.serializedObject.ApplyModifiedProperties();
                captureProperty.serializedObject.Update();
                RebuildCollectionContent(content, captureProperty, capturedElementType);
            }) {
                text = "Add"
            };
            toolbar.Add(addButton);

            RebuildCollectionContent(content, property, elementType);

            foldout.Add(toolbar);
            foldout.Add(content);
            root.Add(foldout);

            return root;
        }

        private static void RebuildCollectionContent(VisualElement content, SerializedProperty collectionProperty, Type elementType)
        {
            content.Clear();

            for (var i = 0; i < collectionProperty.arraySize; i++)
            {
                var index = i;
                var elementProperty = collectionProperty.GetArrayElementAtIndex(index);

                var row = new VisualElement
                {
                    style =
                    {
                        borderBottomWidth = 1,
                        borderBottomColor = new Color(0.2f, 0.2f, 0.2f, 0.3f),
                        paddingBottom = 4,
                        marginBottom = 4
                    }
                };

                var header = new VisualElement { style = { flexDirection = FlexDirection.Row } };
                var title = new Label($"Element {index}") { style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1 } };
                var removeButton = new Button(() =>
                {
                    collectionProperty.DeleteArrayElementAtIndex(index);
                    collectionProperty.serializedObject.ApplyModifiedProperties();
                    collectionProperty.serializedObject.Update();
                    RebuildCollectionContent(content, collectionProperty, elementType);
                })
                {
                    text = "Remove"
                };

                header.Add(title);
                header.Add(removeButton);
                row.Add(header);

                var elementEditor = CreateReferenceEditor(elementProperty, elementType, $"Element {index}");
                row.Add(elementEditor);

                content.Add(row);
            }
        }

        private static ChoiceModel BuildChoiceModel(SerializedProperty targetProperty, Type baseType)
        {
            var result = new ChoiceModel();

            result.Choices.Add("None");
            result.Actions["None"] = () => targetProperty.managedReferenceValue = null;

            foreach (var type in GetImplementationsCached(baseType))
            {
                var label = $"New: {type.Name}";
                result.Choices.Add(label);
                result.Actions[label] = () => targetProperty.managedReferenceValue = Activator.CreateInstance(type);
            }

            foreach (var (id, path, type, captured, managedId) in GetCanonicalReuseSources(targetProperty, baseType))
            {
                if (path == targetProperty.propertyPath) continue;

                var prettyPath = ToRelativeReadablePropertyPath(path, targetProperty.propertyPath);
                var label = $"Reuse: {prettyPath} ({type.Name})";
                if (result.Actions.ContainsKey(label))
                {
                    label = $"{label} #{id}";
                }

                result.Choices.Add(label);
                result.Actions[label] = () => targetProperty.managedReferenceValue = captured;
                result.ReuseManagedIdByLabel[label] = managedId;
            }

            return result;
        }

        private static string GetCurrentChoiceLabel(SerializedProperty property, ChoiceModel model)
        {
            if (property.managedReferenceValue == null) return "None";

            var currentManagedId = property.managedReferenceId;
            foreach (var pair in model.ReuseManagedIdByLabel)
            {
                if (pair.Value == currentManagedId) return pair.Key;
            }

            var currentType = property.managedReferenceValue.GetType();
            var newLabel = $"New: {currentType.Name}";
            return model.Actions.ContainsKey(newLabel) ? newLabel : "None";
        }

        private static IEnumerable<ExistingReferenceCandidate> GetCanonicalReuseSources(SerializedProperty targetProperty, Type baseType)
        {
            using var allIterator = EnumerateExistingReferences(targetProperty, baseType).GetEnumerator();
            if (allIterator.MoveNext() is false) return Enumerable.Empty<ExistingReferenceCandidate>();

            var canonicalByManagedId = new Dictionary<long, ExistingReferenceCandidate>();
            do
            {
                var candidate = allIterator.Current!;
                if (!canonicalByManagedId.TryGetValue(candidate.ManagedId, out var existing))
                {
                    canonicalByManagedId[candidate.ManagedId] = candidate;
                    continue;
                }

                if (string.Compare(candidate.Path, existing.Path, StringComparison.Ordinal) < 0)
                {
                    canonicalByManagedId[candidate.ManagedId] = candidate;
                }
            } while (allIterator.MoveNext());

            return canonicalByManagedId.Values
                .OrderBy(static candidate => candidate.Path, StringComparer.Ordinal);
        }

        private static string ToRelativeReadablePropertyPath(string candidatePath, string targetPath)
        {
            var candidateReadable = ToReadablePropertyPath(candidatePath);
            var targetReadable = ToReadablePropertyPath(targetPath);
            var targetParent = GetParentPath(targetReadable);
            if (string.IsNullOrEmpty(targetParent)) return candidateReadable;

            if (candidateReadable.StartsWith(targetParent + ".", StringComparison.Ordinal)) return candidateReadable[(targetParent.Length + 1) ..];
            if (string.Equals(candidateReadable, targetParent, StringComparison.Ordinal)) return "<parent>";

            return candidateReadable;
        }

        private static string GetParentPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;

            var lastDot = path.LastIndexOf('.');
            return lastDot < 0 ? string.Empty : path[..lastDot];
        }

        private static string ToReadablePropertyPath(string rawPath)
        {
            return string.IsNullOrEmpty(rawPath)
                ? rawPath
                : rawPath.Replace(".Array.data[", "[", StringComparison.Ordinal);
        }

        private static IEnumerable<ExistingReferenceCandidate> EnumerateExistingReferences(SerializedProperty targetProperty, Type baseType)
        {
            var iterator = targetProperty.serializedObject.GetIterator();

            if (!iterator.Next(enterChildren: true)) yield break;

            long id = 0;
            do
            {
                if (iterator.propertyType != SerializedPropertyType.ManagedReference) continue;

                var value = iterator.managedReferenceValue;
                if (value == null) continue;

                var valueType = value.GetType();
                if (!baseType.IsAssignableFrom(valueType)) continue;

                id++;
                yield return new ExistingReferenceCandidate(
                    id,
                    iterator.propertyPath,
                    valueType,
                    value,
                    iterator.managedReferenceId);
            }
            while (iterator.Next(enterChildren: true));
        }

        private static bool IsInspectableBaseType(Type type) => type.IsInterface || type.IsAbstract;

        [MustUseReturnValue]
        private static bool TryGetCollectionElementType(Type type, [NotNullWhen(returnValue: true)] out Type? elementType)
        {
            if (type.IsArray)
            {
                elementType = type.GetElementType();
                return true;
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }

            var iList = type.GetInterfaces()
                .FirstOrDefault(static type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IList<>));

            if (iList != null)
            {
                elementType = iList.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }

        private static Type? GetFieldType(SerializedProperty property)
        {
            var hostType = property.serializedObject.targetObject.GetType();
            var candidate = hostType.GetField(property.propertyPath, Public | NonPublic | Instance);
            if (candidate != null) return candidate.FieldType;

            // Fallback for a nested paths "foo.bar.Array.data[0]"
            return ResolveFieldTypeFromPath(hostType, property.propertyPath);
        }

        private static Type? ResolveFieldTypeFromPath(Type hostType, string propertyPath)
        {
            var currentType = hostType;
            var path = propertyPath.Replace(".Array.data[", "[", StringComparison.Ordinal);
            var elements = path.Split('.');

            foreach (var element in elements)
            {
                if (element.Contains("[", StringComparison.Ordinal))
                {
                    var bracket = element.IndexOf('[', StringComparison.Ordinal);
                    var fieldName = bracket > 0 ? element[..bracket] : element;
                    if (!string.IsNullOrEmpty(fieldName))
                    {
                        var field = currentType.GetField(fieldName, Public | NonPublic | Instance);
                        if (field == null) return null;

                        currentType = field.FieldType;
                    }

                    if (currentType.IsArray)
                    {
                        currentType = currentType.GetElementType()!;
                    }
                    else if (currentType.IsGenericType)
                    {
                        currentType = currentType.GetGenericArguments()[0];
                    }
                    else
                    {
                        return null;
                    }

                    continue;
                }

                var f = currentType.GetField(element, Public | NonPublic | Instance);
                if (f == null) return null;

                currentType = f.FieldType;
            }

            return currentType;
        }

        private static Type[] GetImplementationsCached(Type baseType)
        {
            if (CachedImplementations.TryGetValue(baseType, out var cached)) return cached;

            using var _ = ListPool<Type>.Get(out var types);

            types.AddRange(TypeCache.GetTypesDerivedFrom(baseType)
                .Where(static type => type is
                {
                    IsAbstract: false,
                    IsInterface: false,
                    IsGenericTypeDefinition: false
                }));

            types.RemoveAll(static type => typeof(UnityEngine.Object).IsAssignableFrom(type));

            if (types.Count > 1)
            {
                types.Sort(ByNames.ComparerInstance);
            }

            var result = types.Count > 0 ? types.ToArray() : Array.Empty<Type>();
            CachedImplementations[baseType] = result;
            return result;
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
            return copy.NextVisible(enterChildren: true) && IsChildOf(copy, parent: property);
        }

        private static bool IsChildOf(SerializedProperty child, SerializedProperty parent)
        {
            return child.propertyPath.StartsWith(parent.propertyPath + ".", StringComparison.Ordinal);
        }

        [UnityEditor.Callbacks.DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            CachedImplementations.Clear();
        }

        private sealed class ChoiceModel
        {
            public List<string> Choices { get; } = new();
            public Dictionary<string, Action> Actions { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, long> ReuseManagedIdByLabel { get; } = new(StringComparer.Ordinal);
        }

        private sealed record ExistingReferenceCandidate(long Id, string Path, Type Type, object Value, long ManagedId);

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
