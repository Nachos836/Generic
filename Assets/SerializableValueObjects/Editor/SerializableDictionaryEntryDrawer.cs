﻿#nullable enable

using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace SerializableValueObjects.Editor
{
    [CustomPropertyDrawer(typeof(SerializableDictionary<,>.Entry))]
    internal sealed class SerializableDictionaryEntryDrawer : PropertyDrawer
    {
        private static readonly Color WarningColor = new (a: 0.6f, r:0.9f, g: 0.2f, b: 0.2f);

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var keyProperty = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._key));
            var valueProperty = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._value));
            var duplicateProp = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._duplicated));

            var container = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.FlexStart,
                    flexWrap = Wrap.Wrap,
                    marginLeft = -24
                }
            };

            var duplicateIndicator = new Image
            {
                image = EditorGUIUtility.IconContent("CollabConflict Icon").image,
                tooltip = "An element with the same key already exists in the dictionary.",
                style =
                {
                    alignSelf = Align.FlexStart,
                    alignContent = Align.Center,
                    width = 16,
                    height = 16,
                    marginTop = 2,
                    marginBottom = 2,
                    marginRight = 2
                },
                visible = duplicateProp.boolValue
            };
            var keyField = new PropertyField(keyProperty, string.Empty)
            {
                style =
                {
                    minWidth = Length.Percent(25),
                    maxWidth = Length.Percent(50),
                    marginRight = 4,
                    backgroundColor = duplicateProp.boolValue
                        ? new StyleColor { value = WarningColor }
                        : new StyleColor { keyword = StyleKeyword.Initial }
                }
            };
            var valueField = new PropertyField(valueProperty, string.Empty)
            {
                style =
                {
                    marginLeft = 4,
                    flexGrow = 1
                }
            };

            keyField.RegisterCallback<SerializedPropertyChangeEvent, KeyFieldState>(static (@event, state) =>
            {
                @event.changedProperty.serializedObject.UpdateIfRequiredOrScript();

                state.DuplicateIndicator.visible = state.DuplicateProp.boolValue;
                state.KeyField.style.backgroundColor = state.DuplicateProp.boolValue
                    ? new StyleColor { value = WarningColor }
                    : new StyleColor { keyword = StyleKeyword.Initial };

                @event.Dispose();

            }, new (duplicateIndicator, duplicateProp, keyField));

            valueField.RegisterCallback<GeometryChangedEvent, PropertyField>(static (@event, field) =>
            {
                if (field.Q<Foldout>(className: Foldout.ussClassName) is not { } foldout) return;
                if (foldout.Q<Toggle>(className: Toggle.ussClassName) is not { } toggle) return;

                field.style.paddingLeft = -toggle.resolvedStyle.left;

                @event.Dispose();

            }, valueField);

            container.Add(duplicateIndicator);
            container.Add(keyField);
            container.Add(valueField);

            return container;
        }

        private readonly struct KeyFieldState
        {
            public readonly Image DuplicateIndicator;
            public readonly SerializedProperty DuplicateProp;
            public readonly PropertyField KeyField;

            public KeyFieldState
            (
                Image duplicateIndicator,
                SerializedProperty duplicateProp,
                PropertyField keyField
            ) {
                DuplicateIndicator = duplicateIndicator;
                DuplicateProp = duplicateProp;
                KeyField = keyField;
            }
        }
    }
}
