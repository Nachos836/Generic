#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SerializableValueObjects.Editor.Dictionary
{
    [Serializable]
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>), true)]
    internal sealed class SerializableDictionaryDrawer : PropertyDrawer
    {
        private const string DuplicatedSubclass = "duplicated";

        [SerializeField] private VisualTreeAsset _mainAsset = default!;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var entriesProperty = property.FindPropertyRelative(SerializableDictionary.ListName);
            var root = _mainAsset.CloneTree();
            var headerView = root.Q<VisualElement>(name: "Header");
            var entriesView = root.Q<ListView>(name: "Entries");
            var labelView = headerView.Q<Label>(name: "Label");
            var rawDictionary = (ISerializableDictionaryRawAccess) property.boxedValue;
            var stagingProperty = property.FindPropertyRelative(SerializableDictionary.StagingName);
            if (stagingProperty.arraySize == 0)
            {
                stagingProperty.arraySize = 1;
                stagingProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            var stagingEntryProperty = stagingProperty.GetArrayElementAtIndex(0);
            var stagingKeyProperty =  stagingEntryProperty.FindPropertyRelative(SerializableDictionary.KeyName);

            root.RegisterCallbackOnce<AttachToPanelEvent>(_ =>
            {
                labelView.text = property.displayName;

                entriesView.itemsSource = rawDictionary.BackingCollection;
                entriesView.bindItem = BindEntry;
                entriesView.itemsRemoved += OnItemsRemoved;
                entriesView.itemsAdded += OnItemsAdded;
            });

            root.RegisterCallbackOnce<DetachFromPanelEvent>(_ =>
            {
                entriesView.itemsAdded -=  OnItemsAdded;
                entriesView.itemsRemoved -= OnItemsRemoved;
                entriesView.bindItem = null;
                entriesView.itemsSource = null;

                labelView.text = string.Empty;
            });

            return root;

            void BindEntry(VisualElement entryView, int current)
            {
                var entryProperty = entriesProperty.GetArrayElementAtIndex(current);
                var keyProperty = entryProperty.FindPropertyRelative(SerializableDictionary.KeyName);
                var valueProperty = entryProperty.FindPropertyRelative(SerializableDictionary.ValueName);

                var keyField = entryView.Q<PropertyField>(name: "KeyField");
                var valueField = entryView.Q<PropertyField>(name: "ValueField");

                keyField.Unbind();
                valueField.Unbind();

                keyField.BindProperty(keyProperty);
                valueField.BindProperty(valueProperty);

                var origin = entryView.parent.parent;
                var duplicated = false;
                keyField.UnregisterCallback<SerializedPropertyChangeEvent>(DetectDuplicates);
                keyField.RegisterCallback<SerializedPropertyChangeEvent>(DetectDuplicates);
                keyField.userData = keyProperty.boxedValue;

                return;

                void DetectDuplicates(SerializedPropertyChangeEvent @event)
                {
                    var changed = @event.changedProperty;

                    if (rawDictionary.CheckUpdatedKey(current, changed.boxedValue))
                    {
                        if (duplicated is false)
                        {
                            keyField.userData = changed.boxedValue;
                            return;
                        }

                        if (changed.propertyPath != stagingKeyProperty.propertyPath) return;

                        origin.EnableInClassList(DuplicatedSubclass, enable: duplicated = false);

                        keyField.Unbind();

                        keyProperty.boxedValue = changed.boxedValue;
                        changed.serializedObject.ApplyModifiedPropertiesWithoutUndo();

                        keyField.BindProperty(keyProperty);
                    }
                    else
                    {
                        if (duplicated) return;
                        if (changed.propertyPath == stagingEntryProperty.propertyPath) return;

                        origin.EnableInClassList(DuplicatedSubclass, enable: duplicated = true);

                        keyField.Unbind();

                        stagingKeyProperty.boxedValue = changed.boxedValue;
                        keyProperty.boxedValue = keyField.userData;
                        changed.serializedObject.ApplyModifiedPropertiesWithoutUndo();

                        keyField.BindProperty(stagingKeyProperty);
                    }
                }
            }

            void OnItemsAdded(IEnumerable<int> indexes)
            {
                using var indexesEnumerator = indexes.GetEnumerator();
                if (indexesEnumerator.MoveNext() is false) return;

                do
                {
                    entriesProperty.InsertArrayElementAtIndex(indexesEnumerator.Current);

                } while (indexesEnumerator.MoveNext());
                entriesProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            void OnItemsRemoved(IEnumerable<int> indexes)
            {
                using var indexesEnumerator = indexes.OrderByDescending(static item => item)
                    .GetEnumerator();
                if (indexesEnumerator.MoveNext() is false) return;

                do
                {
                    var current = indexesEnumerator.Current;
                    entriesProperty.DeleteArrayElementAtIndex(current);

                } while (indexesEnumerator.MoveNext());
                entriesProperty.serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
