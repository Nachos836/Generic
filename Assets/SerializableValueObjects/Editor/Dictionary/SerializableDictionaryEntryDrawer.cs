#nullable enable

using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SerializableValueObjects.Editor.Dictionary
{
    [Serializable]
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>.Entry))]
    internal sealed class SerializableDictionaryEntryDrawer : PropertyDrawer
    {
        private const string DuplicatedSubclass = "duplicated";

        [SerializeField] private VisualTreeAsset _asset = default!;
        [SerializeField] private StyleSheet _style = default!;

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var keyProperty = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._key));
            var valueProperty = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._value));
            var duplicateProp = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._duplicated));

            var root = _asset.CloneTree();
            var container = root.Q<VisualElement>(className: "container");
            var duplicateIndicator = container.Q<VisualElement>(className: "conflict-icon");
            var keyField = container.Q<PropertyField>(className: "key-field");
            var valueField = container.Q<PropertyField>(className: "value-field");

            container.styleSheets.Add(_style);

            keyField.BindProperty(keyProperty);
            valueField.BindProperty(valueProperty);

            SetDuplicateState(duplicateIndicator, container, duplicateProp.boolValue);

            keyField.RegisterCallback<SerializedPropertyChangeEvent, KeyFieldState>(static (@event, state) =>
            {
                @event.changedProperty.serializedObject.UpdateIfRequiredOrScript();
                SetDuplicateState(state.DuplicateIndicator, state.Container, state.DuplicateProp.boolValue);

            }, new (duplicateIndicator, container, duplicateProp));

            return container;

            static void SetDuplicateState(VisualElement indicator, VisualElement container, bool duplicated)
            {
                indicator.EnableInClassList(DuplicatedSubclass, duplicated);
                container.EnableInClassList(DuplicatedSubclass, duplicated);
            }
        }

        private readonly struct KeyFieldState
        {
            public readonly VisualElement DuplicateIndicator;
            public readonly VisualElement Container;
            public readonly SerializedProperty DuplicateProp;

            public KeyFieldState
            (
                VisualElement duplicateIndicator,
                VisualElement container,
                SerializedProperty duplicateProp
            ) {
                DuplicateIndicator = duplicateIndicator;
                Container = container;
                DuplicateProp = duplicateProp;
            }
        }
    }
}
