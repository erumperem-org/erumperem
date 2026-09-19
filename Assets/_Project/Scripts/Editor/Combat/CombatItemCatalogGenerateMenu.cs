#if UNITY_EDITOR
using Erumperem.Combat.Authoring;
using Game.Core.Items;
using UnityEditor;
using UnityEngine;

namespace Erumperem.Editor.Combat
{
    /// <summary>
    /// Creates one CombatItemAsset per factory catalog entry so designers can duplicate and tweak.
    /// Then run Erumperem/Combat/Export Catalog to write items.json.
    /// </summary>
    public static class CombatItemCatalogGenerateMenu
    {
        private const string ItemsFolder = "Assets/_Project/ScriptableObjects/Combat/Items";

        [MenuItem("Erumperem/Combat/Generate Default Combat Items")]
        public static void GenerateDefaultCombatItems()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/ScriptableObjects/Combat"))
            {
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects", "Combat");
            }

            if (!AssetDatabase.IsValidFolder(ItemsFolder))
            {
                AssetDatabase.CreateFolder("Assets/_Project/ScriptableObjects/Combat", "Items");
            }

            var createdCount = 0;
            var updatedCount = 0;
            foreach (var itemDefinition in CombatItemCatalogFactory.CreateDefaultCatalog())
            {
                var assetPath = $"{ItemsFolder}/{itemDefinition.Id}.asset";
                var existingAsset = AssetDatabase.LoadAssetAtPath<CombatItemAsset>(assetPath);
                if (existingAsset == null)
                {
                    existingAsset = ScriptableObject.CreateInstance<CombatItemAsset>();
                    existingAsset.ApplyFactoryDefinition(itemDefinition);
                    AssetDatabase.CreateAsset(existingAsset, assetPath);
                    createdCount++;
                    continue;
                }

                existingAsset.ApplyFactoryDefinition(itemDefinition);
                EditorUtility.SetDirty(existingAsset);
                updatedCount++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"Generate Default Combat Items: created {createdCount}, updated {updatedCount} under {ItemsFolder}. " +
                "Drag assets into ItemRegistry for inventory loot. Run Export Catalog to write items.json.");
        }
    }
}
#endif
