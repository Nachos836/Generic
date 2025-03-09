using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Generic.Editor.Drawer
{
    using SerializableValueObjects;

    [CustomPropertyDrawer(typeof(SerializableDictionary<,>.Entry))]
    internal sealed class SerializableDictionaryEntryDrawer : PropertyDrawer
    {
        private static readonly GUIContent InvalidKeyContent = new (string.Empty, "An element with the same key already exists in the dictionary.");
        private static readonly RectOffset IconRectOffset = new (26, 0, -1, 0);
        private static readonly Vector2 IconSize = new (16, 16);

        private static float LineHeightWithSpacing => EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            GetRelativeProps(property, out var keyProp, out var valueProp, out var duplicateProp);
            SplitRect(position, out var keyRect, out var valueRect);

            DrawInvalidKeyIndicator(keyRect, duplicateProp.boolValue);
            DrawKeyValuePairField(keyRect, keyProp, "Key");
            DrawKeyValuePairField(valueRect, valueProp, "Value");

            EditorGUI.EndProperty();
        }

        private static void GetRelativeProps
        (
            SerializedProperty property,
            out SerializedProperty keyProp,
            out SerializedProperty valueProp,
            out SerializedProperty duplicateProp
        ) {
            keyProp = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._key));
            valueProp = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._value));
            duplicateProp = property.FindPropertyRelative(nameof(SerializableDictionary<int, int>.Entry._duplicated));
        }

        private static void SplitRect(Rect position, out Rect keyRect, out Rect valueRect)
        {
            keyRect = position;
            valueRect = position;
            keyRect.width /= 3f;
            var valueRectPadding = EditorStyles.foldout.padding.left - EditorStyles.label.padding.left;
            valueRect.xMin = keyRect.xMax + valueRectPadding;
        }

        private static void DrawKeyValuePairField(Rect position, SerializedProperty property, string label)
        {
            using (new WideModeScope(GetWideModeFor(property)))
            {
                EditorGUIUtility.labelWidth = position.width / 3;

                if (ShouldHideFoldoutProperty(property))
                {
                    PropertyDrawerUtility.DrawPropertyChildren(position, property);
                    return;
                }

                position.height = EditorGUI.GetPropertyHeight(property);
                EditorGUI.PropertyField(position, property, CanHaveFoldout(property) ? new GUIContent(label) : GUIContent.none, true);
            }
        }

        private static void DrawInvalidKeyIndicator(Rect keyRect, bool isInvalid)
        {
            if (InvalidKeyContent.image == null)
            {
                InvalidKeyContent.image = EditorGUIUtility.IconContent("CollabConflict Icon").image;
            }

            var iconRect = IconRectOffset.Add(keyRect);
            iconRect.size = IconSize;
            using (new EditorGUIUtility.IconSizeScope(IconSize))
            {
                var content = isInvalid ? InvalidKeyContent : GUIContent.none;
                EditorGUI.LabelField(iconRect, content);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            GetRelativeProps(property, out var keyProp, out var valueProp, out _);
            var height = Mathf.Max(GetHeightFor(keyProp), GetHeightFor(valueProp));
            return height;
        }

        private static float GetHeightFor(SerializedProperty property)
        {
            using (new WideModeScope(GetWideModeFor(property)))
            {
                var height = EditorGUI.GetPropertyHeight(property) + GetHeightOffsetFor(property);
                if (!ShouldHideFoldoutProperty(property)) return height;

                property.isExpanded = true;
                return EditorGUI.GetPropertyHeight(property) + GetHeightOffsetFor(property) - LineHeightWithSpacing;
            }
        }

        /// <summary>
        /// Some properties have a strange implementations/bugs that return extra unused space from EditorGUI.GetPropertyHeight
        /// </summary>
        private static float GetHeightOffsetFor(SerializedProperty property)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Vector4 when !EditorGUIUtility.wideMode:
                case SerializedPropertyType.Rect or SerializedPropertyType.RectInt : return -LineHeightWithSpacing;
                default: return 0;
            }
        }

        private static bool ShouldHideFoldoutProperty(SerializedProperty property)
        {
            return property.isArray is false
                   && CanHaveFoldout(property)
                   && PropertyDrawerUtility.TryGetPropertyDrawer(property, out _, out var failed) is false
                   && failed is not true;
        }

        private static bool GetWideModeFor(SerializedProperty property)
        {
            return property.propertyType is SerializedPropertyType.Bounds
                or SerializedPropertyType.BoundsInt
                or SerializedPropertyType.Vector4;
        }

        private static bool CanHaveFoldout(SerializedProperty prop)
        {
            return prop.propertyType is SerializedPropertyType.Generic
                or SerializedPropertyType.Vector4
                or SerializedPropertyType.Bounds
                or SerializedPropertyType.BoundsInt;
        }

        private readonly ref struct WideModeScope
        {
            private readonly bool _previousValue;

            public WideModeScope(bool value)
            {
                _previousValue = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = value;
            }

            public void Dispose()
            {
                EditorGUIUtility.wideMode = _previousValue;
            }
        }
    }

    internal static class PropertyDrawerUtility
    {
        private static readonly MethodInfo GetFieldInfoFromPropertyMethodInfo;
        private static readonly MethodInfo GetDrawerTypeForPropertyAndTypeMethodInfo;

        static PropertyDrawerUtility()
        {
            var scriptAttributeUtilityType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ScriptAttributeUtility");
            GetFieldInfoFromPropertyMethodInfo = scriptAttributeUtilityType?.GetMethod("GetFieldInfoFromProperty", BindingFlags.Static | BindingFlags.NonPublic);
            GetDrawerTypeForPropertyAndTypeMethodInfo = scriptAttributeUtilityType?.GetMethod("GetDrawerTypeForPropertyAndType", BindingFlags.Static | BindingFlags.NonPublic);
        }

        public static void DrawPropertyChildren(Rect position, SerializedProperty prop)
        {
            var endProperty = prop.GetEndProperty();
            var childrenDepth = prop.depth + 1;
            while (prop.NextVisible(true) && SerializedProperty.EqualContents(prop, endProperty) is false)
            {
                if (prop.depth != childrenDepth) continue;

                position.height = EditorGUI.GetPropertyHeight(prop);
                EditorGUI.PropertyField(position, prop, true);
                position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
            }
        }

        /// <summary>
        /// Get custom property drawer for serialized property
        /// </summary>
        /// <param name="property">Property drawer target</param>
        /// <param name="drawer">Property drawer result</param>
        /// <param name="failed">If method failed due to reflection</param>
        /// <returns></returns>
        public static bool TryGetPropertyDrawer(SerializedProperty property, out PropertyDrawer drawer, out bool failed)
        {
            try
            {
                GetFieldInfoFromProperty(property, out var type);
                var drawerType = GetDrawerTypeForPropertyAndType(property, type);
                if (drawerType == null)
                {
                    drawer = null;
                    failed = false;
                    return false;
                }

                drawer = (PropertyDrawer)Activator.CreateInstance(drawerType);
                failed = drawer == null;
                return failed is not true;
            }
            catch
            {
                drawer = null;
                return failed = false;
            }
        }

        private static FieldInfo GetFieldInfoFromProperty(SerializedProperty property, out Type type)
        {
            var parameters = new object[] { property, null };
            var fieldInfo = GetFieldInfoFromPropertyMethodInfo.Invoke(null, parameters);
            type = (Type) parameters[1];
            return (FieldInfo) fieldInfo;
        }

        private static Type GetDrawerTypeForPropertyAndType(SerializedProperty property, Type type)
        {
            var result = GetDrawerTypeForPropertyAndTypeMethodInfo.Invoke(null, new object[] { property, type });
            return (Type) result;
        }
    }
}
