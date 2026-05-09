#if UNITY_EDITOR
using Erumperem.Combat.Tokens;
using UnityEditor;
using UnityEngine;

namespace Erumperem.Editor.Combat
{
    public static class TokenVisualCatalogMenu
    {
        private const string DefaultCatalogPath = "Assets/_Project/Data/Combat/DefaultTokenVisualCatalog.asset";

        [MenuItem("Erumperem/Combat/Create Default Token Visual Catalog")]
        private static void CreateCatalog()
        {
            var folder = System.IO.Path.GetDirectoryName(DefaultCatalogPath)?.Replace("\\", "/");
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                var parts = folder.Split('/');
                var current = "Assets";
                for (var i = 1; i < parts.Length; i++)
                {
                    var next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }

                    current = next;
                }
            }

            var catalog = ScriptableObject.CreateInstance<TokenVisualCatalog>();
            AssetDatabase.CreateAsset(catalog, DefaultCatalogPath);
            AssetDatabase.SaveAssets();
            EditorGUIUtility.PingObject(catalog);
        }
    }
}
#endif
