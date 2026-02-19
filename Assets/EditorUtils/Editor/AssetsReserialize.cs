using UnityEditor;

namespace EditorUtils.Editor
{
    internal static class AssetsReserialize
    {
        [MenuItem("Tools/Force Reserialize Assets")]
        public static void ForceReserializeAssets()
        {
            AssetDatabase.ForceReserializeAssets();
        }
    }
}
