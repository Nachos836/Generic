using UnityEditor;

namespace Generic.Editor
{
    internal static class AssetsReserialize
    {
        [MenuItem("Tools/Force Reserialize Assets")]
        private static void ForceReserializeAssets()
        {
            AssetDatabase.ForceReserializeAssets();
        }
    }
}
