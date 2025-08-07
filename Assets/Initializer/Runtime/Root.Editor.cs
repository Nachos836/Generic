#if UNITY_EDITOR

#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Initializer
{
    [SuppressMessage("Domain reload", "UDR0002:Domain Reload Analyzer")]
    [SuppressMessage("ReSharper", "SuspiciousTypeConversion.Global")]
    [SuppressMessage("ReSharper", "Unity.NoNullPatternMatching")]
    partial class Root
    {
        [MenuItem("Assets/Create/Initializer/Root")]
        private static void CreateAsset()
        {
            var path = EditorUtility.SaveFilePanelInProject
            (
                title: "Create Root Initializer",
                defaultName: "Root",
                extension: "asset",
                message: string.Empty
            );

            if (string.IsNullOrEmpty(path)) return;

            var newRoot = CreateInstance<Root>();
            AssetDatabase.CreateAsset(newRoot, path);

            var assets = PlayerSettings.GetPreloadedAssets();

            if (assets.Any(HasRoot))
            {
                var preloadedAssets = assets.ToList();
                preloadedAssets.RemoveAll(HasRoot);
                preloadedAssets.Add(newRoot);
                PlayerSettings.SetPreloadedAssets(preloadedAssets.ToArray());
            }
            else
            {
                Array.Resize(ref assets, assets.Length + 1);
                assets[^1] = newRoot;
                PlayerSettings.SetPreloadedAssets(assets);
            }

            return;

            static bool HasRoot(UnityEngine.Object candidate) => candidate && candidate is Root;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void Initialize()
        {
            if (TrySetInstance(ref _instance) is false) return;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            return;

            static bool TrySetInstance([NotNullWhen(returnValue: true)] ref Root? instance)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode is false) return false;

                Debug.Log("EditorAwake is called!");

                foreach (ref var candidate in PlayerSettings.GetPreloadedAssets().AsSpan())
                {
                    var initializable = candidate as Root;
                    if (!initializable) continue;

                    instance = initializable;
                    return true;
                }

                return false;
            }

            static void OnPlayModeStateChanged(PlayModeStateChange change)
            {
                switch (change)
                {
                    case PlayModeStateChange.EnteredPlayMode when _instance:
                    {
                        _instance = Enable(root: _instance);
                        return;
                    }
                    case PlayModeStateChange.ExitingPlayMode when _instance:
                    {
                        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

                        _instance = Disable(root: _instance);
                        return;
                    }
                    default: return;
                }
            }
        }
    }
}

#endif
