using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace EditorUtils.Editor
{
    internal static class ForceReserializeSelectedAssetTool
    {
        private const string MenuPath = "Assets/Force Reserialize";

        private static IEnumerable<Object> Targets
        {
            get => Selection.objects.Append(Selection.activeObject)
                .Where(static candidate => candidate)
                .Distinct();
        }

        [MenuItem(MenuPath, isValidateFunction: false)]
        private static void Execute()
        {
            AssetDatabase.ForceReserializeAssets(Targets.Select(AssetDatabase.GetAssetPath));
        }

        [MenuItem(MenuPath, isValidateFunction: true)]
        private static bool Validate()
        {
            using var targets = Targets.GetEnumerator();

            if (targets.MoveNext() is false) return false;

            do if (AssetDatabase.Contains(targets.Current) is not true) return false;
            while (targets.MoveNext());

            return true;
        }
    }
}
