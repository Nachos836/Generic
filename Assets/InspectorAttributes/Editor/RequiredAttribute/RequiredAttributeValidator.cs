using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.SceneManagement;

using static System.Reflection.BindingFlags;

using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace InspectorAttributes.Editor.RequiredAttribute
{
    [InitializeOnLoad]
    internal sealed class RequiredAttributeValidator : IPreprocessBuildWithReport
    {
        int IOrderedCallback.callbackOrder => 0;

        static RequiredAttributeValidator()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MenuItem("Tools/Inspector Attributes/Validate Required Fields")]
        public static void ValidateEntireProject()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() is false) return;

            var allValid = true;
            var errorCount = 0;

            try
            {
                allValid &= ValidateAllPrefabs(ref errorCount);
                allValid &= ValidateAllScenes(ref errorCount);
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("Validation Error", exception.Message, "OK");

                return;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            if (allValid)
            {
                EditorUtility.DisplayDialog("Validation Complete", "No missing [Required] fields found in the entire project!", "Awesome");
            }
            else
            {
                EditorUtility.DisplayDialog("Validation Failed", $"Found issues in {errorCount} files/scenes. Check the console for details.", "OK");
            }

            return;

            static bool ValidateAllPrefabs(ref int errorCount)
            {
                var allValid = true;
                var prefabGuids = AssetDatabase.FindAssets("t:Prefab");
                float totalItems = prefabGuids.Length;

                for (var i = 0; i < prefabGuids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                    if (IsAssetEditable(path) is false) continue;

                    EditorUtility.DisplayProgressBar("Validating Project", $"Checking Prefab: {path}", i / totalItems * 0.5f);

                    var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefabRoot == null) continue;

                    var components = prefabRoot.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
                    if (CheckComponents(components, path, isPrefabAsset: true)) continue;

                    allValid = false;
                    errorCount++;
                }

                return allValid;
            }

            static bool ValidateAllScenes(ref int errorCount)
            {
                var allValid = true;
                var sceneGuids = AssetDatabase.FindAssets("t:Scene");
                var originalScene = SceneManager.GetActiveScene().path;
                var totalItems = sceneGuids.Length;

                for (var i = 0; i < sceneGuids.Length; i++)
                {
                    var path = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                    if (IsAssetEditable(path) is false) continue;

                    EditorUtility.DisplayProgressBar("Validating Project", $"Checking Scene: {path}", progress: 0.5f + i / (totalItems * 0.5f));

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    var roots = scene.GetRootGameObjects();
                    foreach (ref readonly var root in roots.AsSpan())
                    {
                        var components = root.GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
                        if (CheckComponents(components, path, false)) continue;

                        allValid = false;
                        errorCount++;
                    }
                }

                if (string.IsNullOrEmpty(originalScene) is false)
                {
                    EditorSceneManager.OpenScene(originalScene);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
                }

                return allValid;
            }
        }

        void IPreprocessBuildWithReport.OnPreprocessBuild(BuildReport report)
        {
            if (ValidateActiveScenes() is false)
            {
                throw new BuildFailedException("Build cancelled due to missing [Required] references.");
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode) return;
            if (ValidateActiveScenes()) return;

            EditorApplication.isPlaying = false;
            Debug.LogError("<b>[Required Validator]</b> Play Mode cancelled due to missing required references.");
            EditorUtility.DisplayDialog("Validation Failed", "Play Mode blocked!\nSome [Required] fields are missing references. Check the Console for details.", "OK");
        }

        private static bool ValidateActiveScenes()
        {
            var allValid = true;
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded is false) continue;

                var roots = scene.GetRootGameObjects();
                foreach (ref readonly var root in roots.AsSpan())
                {
                    var components = root.GetComponentsInChildren<MonoBehaviour>(true);
                    if (CheckComponents(components, scene.path, isPrefabAsset: false) is false)
                    {
                        allValid = false;
                    }
                }
            }
            return allValid;
        }

        private static bool IsAssetEditable(string assetPath)
        {
            var info = PackageInfo.FindForAssetPath(assetPath);
            if (info == null) return true; // info folder is in Assets -> true

            return info.source is PackageSource.Embedded or PackageSource.Local;
        }

        private static bool CheckComponents(MonoBehaviour[] components, string contextName, bool isPrefabAsset)
        {
            var allValid = true;

            foreach (var candidate in components)
            {
                if (candidate == null) continue;

                var type = candidate.GetType();
                var fields = type.GetFields(bindingAttr: Instance | Public | NonPublic);

                foreach (var field in fields)
                {
                    if (Attribute.IsDefined(field, typeof(InspectorAttributes.RequiredAttribute)) is false) continue;

                    var value = field.GetValue(candidate);
                    var isMissing = false;

                    if (value == null)
                    {
                        isMissing = true;
                    }
                    else if (value.Equals(null))
                    {
                        isMissing = true;
                    }
                    else if (field.FieldType == typeof(string) && string.IsNullOrEmpty((string)value))
                    {
                        isMissing = true;
                    }

                    if (isMissing)
                    {
                        allValid = false;

                        var objectPath = isPrefabAsset ? contextName + " -> " + GetPath(candidate.transform) : GetPath(candidate.transform);

                        Debug.LogError($"[Required] Field <b>'{field.Name}'</b> is missing on: <b>{objectPath}</b> (Context: {contextName})", candidate);
                    }
                }
            }

            return allValid;
        }

        private static string GetPath(Transform transform)
        {
            var path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = transform.name + "/" + path;
            }
            return path;
        }
    }
}
