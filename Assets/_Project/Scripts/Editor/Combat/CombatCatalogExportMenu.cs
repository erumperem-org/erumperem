#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Erumperem.Combat.Authoring;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Items;
using Game.Core.Models;
using UnityEditor;
using UnityEngine;

namespace Erumperem.Editor.Combat
{
    /// <summary>
    /// Writes skills.json, passives.json, and skill_trees.json from CombatAbilityAssets
    /// into Assets/StreamingAssets/Data. Never overwrites enemies.json.
    /// Existing JSON entries that have no matching asset are kept (Horse Boss, current kits).
    /// </summary>
    public static class CombatCatalogExportMenu
    {
        private const string StreamingAssetsDataFolder = "Assets/StreamingAssets/Data";
        private const string AuthoringFolder = "Assets/_Project/ScriptableObjects/Combat";
        private const string ResourcesFolder = "Assets/_Project/Resources";

        [MenuItem("Erumperem/Combat/Export Catalog")]
        public static void ExportCatalog()
        {
            var abilityAssets = LoadAbilityAssets();
            if (abilityAssets.Count == 0)
            {
                var catalogDirectoryForItemsOnly = Path.Combine(
                    Directory.GetParent(Application.dataPath)!.FullName,
                    "Assets",
                    "StreamingAssets",
                    "Data");
                Directory.CreateDirectory(catalogDirectoryForItemsOnly);
                ExportCombatItems(catalogDirectoryForItemsOnly, out var exportedItemCountWhenNoAbilities);
                AssetDatabase.Refresh();
                Debug.LogWarning(
                    "Export Catalog: no CombatAbilityAsset found. Skills/passives/trees left unchanged. " +
                    $"Items written: {exportedItemCountWhenNoAbilities}.");
                return;
            }

            var duplicateAbilityIds = abilityAssets
                .GroupBy(asset => asset.AbilityId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToList();
            if (duplicateAbilityIds.Count > 0)
            {
                Debug.LogError(
                    "Export Catalog aborted: duplicate abilityId values: " +
                    string.Join(", ", duplicateAbilityIds));
                return;
            }

            var catalogDirectory = Path.Combine(
                Directory.GetParent(Application.dataPath)!.FullName,
                "Assets",
                "StreamingAssets",
                "Data");
            Directory.CreateDirectory(catalogDirectory);

            var skillsPath = Path.Combine(catalogDirectory, "skills.json");
            var passivesPath = Path.Combine(catalogDirectory, "passives.json");
            var skillTreesPath = Path.Combine(catalogDirectory, "skill_trees.json");

            var skills = File.Exists(skillsPath)
                ? CombatDataLoader.LoadSkills(skillsPath).ToList()
                : new List<SkillDefinition>();
            var passives = File.Exists(passivesPath)
                ? CombatDataLoader.LoadPassives(passivesPath).ToList()
                : new List<PassiveDefinition>();
            var skillTrees = File.Exists(skillTreesPath)
                ? CombatDataLoader.LoadSkillTrees(skillTreesPath).ToList()
                : new List<CharacterSkillTreesDefinition>();

            var exportedActiveCount = 0;
            var exportedPassiveCount = 0;
            var exportedTreeNodeCount = 0;

            foreach (var abilityAsset in abilityAssets)
            {
                if (string.IsNullOrWhiteSpace(abilityAsset.AbilityId))
                {
                    Debug.LogError($"Export Catalog aborted: asset '{abilityAsset.name}' has an empty abilityId.", abilityAsset);
                    return;
                }

                try
                {
                    if (abilityAsset.IsActiveAbility)
                    {
                        skills = CombatCatalogWriter.UpsertSkill(skills, abilityAsset.ToRuntimeSkillDefinition()).ToList();
                        exportedActiveCount++;
                    }
                    else if (abilityAsset.IsPassiveAbility)
                    {
                        passives = CombatCatalogWriter.UpsertPassive(passives, abilityAsset.ToRuntimePassiveDefinition()).ToList();
                        exportedPassiveCount++;
                    }

                    if (abilityAsset.Placement == CombatAbilityPlacement.TreeNode)
                    {
                        if (!CombatCatalogWriter.TryUpsertSkillTreeNode(
                                skillTrees,
                                abilityAsset.OwnerCharacterId,
                                abilityAsset.TreeIndex,
                                abilityAsset.TierIndex,
                                abilityAsset.ToRuntimeSkillTreeNodeDefinition(),
                                out var treeFailureMessage))
                        {
                            Debug.LogWarning(
                                $"Export Catalog: tree node '{abilityAsset.AbilityId}' not written to skill_trees.json — {treeFailureMessage}",
                                abilityAsset);
                        }
                        else
                        {
                            exportedTreeNodeCount++;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        $"Export Catalog aborted on '{abilityAsset.name}': {exception.Message}",
                        abilityAsset);
                    return;
                }
            }

            CombatCatalogWriter.WriteSkills(skillsPath, skills);
            CombatCatalogWriter.WritePassives(passivesPath, passives);
            CombatCatalogWriter.WriteSkillTrees(skillTreesPath, skillTrees);
            ExportCombatItems(catalogDirectory, out var exportedItemCount);
            AssetDatabase.Refresh();

            Debug.Log(
                "Export Catalog wrote StreamingAssets/Data (enemies.json untouched). " +
                $"Actives upserted: {exportedActiveCount}, passives upserted: {exportedPassiveCount}, " +
                $"tree nodes upserted: {exportedTreeNodeCount}, items upserted: {exportedItemCount}. " +
                $"Assets scanned: {abilityAssets.Count}.");
        }

        private static void ExportCombatItems(string catalogDirectory, out int exportedItemCount)
        {
            exportedItemCount = 0;
            var itemsPath = Path.Combine(catalogDirectory, "items.json");
            var items = File.Exists(itemsPath)
                ? CombatDataLoader.LoadItems(itemsPath).ToList()
                : CombatItemCatalogFactory.CreateDefaultCatalog().ToList();

            var itemAssets = LoadItemAssets();
            if (itemAssets.Count == 0 && items.Count == 0)
            {
                items = CombatItemCatalogFactory.CreateDefaultCatalog().ToList();
            }

            foreach (var itemAsset in itemAssets)
            {
                if (string.IsNullOrWhiteSpace(itemAsset.ItemId))
                {
                    Debug.LogError($"Export Catalog: CombatItemAsset '{itemAsset.name}' has an empty itemId.", itemAsset);
                    continue;
                }

                items = CombatCatalogWriter.UpsertItem(items, itemAsset.ToRuntimeDefinition()).ToList();
                exportedItemCount++;
            }

            if (exportedItemCount == 0 && items.Count == 0)
            {
                items = CombatItemCatalogFactory.CreateDefaultCatalog().ToList();
            }

            CombatCatalogWriter.WriteItems(itemsPath, items);
            if (exportedItemCount == 0)
            {
                exportedItemCount = items.Count;
            }
        }

        private static List<CombatItemAsset> LoadItemAssets()
        {
            var searchFolders = new List<string>();
            if (AssetDatabase.IsValidFolder(AuthoringFolder))
            {
                searchFolders.Add(AuthoringFolder);
            }

            if (searchFolders.Count == 0)
            {
                searchFolders.Add("Assets/_Project");
            }

            var itemAssets = new List<CombatItemAsset>();
            var assetGuids = AssetDatabase.FindAssets("t:CombatItemAsset", searchFolders.ToArray());
            foreach (var assetGuid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                var itemAsset = AssetDatabase.LoadAssetAtPath<CombatItemAsset>(assetPath);
                if (itemAsset != null)
                {
                    itemAssets.Add(itemAsset);
                }
            }

            return itemAssets;
        }

        private static List<CombatAbilityAsset> LoadAbilityAssets()
        {
            var searchFolders = new List<string>();
            if (AssetDatabase.IsValidFolder(AuthoringFolder))
            {
                searchFolders.Add(AuthoringFolder);
            }

            if (AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                searchFolders.Add(ResourcesFolder);
            }

            if (searchFolders.Count == 0)
            {
                searchFolders.Add("Assets/_Project");
            }

            var abilityAssets = new List<CombatAbilityAsset>();
            var assetGuids = AssetDatabase.FindAssets("t:CombatAbilityAsset", searchFolders.ToArray());
            foreach (var assetGuid in assetGuids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                var abilityAsset = AssetDatabase.LoadAssetAtPath<CombatAbilityAsset>(assetPath);
                if (abilityAsset != null)
                {
                    abilityAssets.Add(abilityAsset);
                }
            }

            return abilityAssets;
        }
    }
}
#endif
