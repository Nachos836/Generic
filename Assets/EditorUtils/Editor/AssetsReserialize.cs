using UnityEditor;

namespace EditorUtils.Editor
{
    public static class AssetsReserialize
    {
        [MenuItem("Tools/Force Reserialize Assets")]
        public static void ForceReserializeAssets()
        {
            AssetDatabase.ForceReserializeAssets();
        }
    }
}
