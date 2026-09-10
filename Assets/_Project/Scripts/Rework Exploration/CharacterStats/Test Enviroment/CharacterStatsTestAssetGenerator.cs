#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Core.Storage;
using Core.Exploration.Items;

namespace Core.CharacterStats.Testing
{
    /// <summary>
    /// Editor menu items that generate a small set of clearly-marked TEST
    /// item assets (not real game balance — see the "TEST_" id prefix and
    /// "Test-only asset" description on everything created here) for
    /// quickly building a test scene without manually configuring assets
    /// one field at a time in the Inspector.
    /// </summary>
    public static class CharacterStatsTestAssetGenerator
    {
        private const string OutputFolder = "Assets/_TestData/CharacterStats";

        [MenuItem("Tools/Character Stats Testing/Generate Sample Items")]
        public static void GenerateSampleItems()
        {
            EnsureFolderExists();

            CreateStatusItem(
                assetName: "TestItem_HealthPotionSmall",
                storageableId: "TEST_HEALTH_POTION_SMALL",
                displayName: "Test Health Potion (Small)",
                description: "Test-only asset. Restores a small amount of health.",
                rarity: ItemRarity.Common,
                modifiers: new[] { (StatType.Health, StatModifierType.Absolute, 1f) });

            CreateStatusItem(
                assetName: "TestItem_PaladinoExtreme",
                storageableId: "TEST_PALADINO_EXTREME",
                displayName: "Test Paladino (Extreme)",
                description: "Test-only asset. Boosts all three stats at once.",
                rarity: ItemRarity.Legendary,
                modifiers: new[]
                {
                    (StatType.Health, StatModifierType.Absolute, 30f),
                    (StatType.Defense, StatModifierType.Absolute, 30f),
                    (StatType.Critical, StatModifierType.Relative, 15f)
                });

            CreateSkillTreeResetItem(
                assetName: "TestItem_SkillTreeResetGeneral",
                storageableId: "TEST_SKILLTREE_RESET_GENERAL",
                displayName: "Test Skill Tree Reset (General)",
                description: "Test-only asset. Resets the general skill tree.",
                rarity: ItemRarity.Legendary,
                scope: SkillTreeResetScope.General,
                targetCharacterId: "");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterStatsTestAssetGenerator] Sample items generated in '{OutputFolder}'.");
        }

        private static void CreateStatusItem(
            string assetName, string storageableId, string displayName, string description,
            ItemRarity rarity, (StatType stat, StatModifierType type, float magnitude)[] modifiers)
        {
            var asset = ScriptableObject.CreateInstance<StatusModifierItem>();
            var so = new SerializedObject(asset);

            so.FindProperty("_storageableId").stringValue = storageableId;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue = description;
            so.FindProperty("_rarity").enumValueIndex = (int)rarity;

            SetDefaultStackableStrategy(so);

            var modifiersProp = so.FindProperty("_modifiers");
            modifiersProp.arraySize = modifiers.Length;

            for (int i = 0; i < modifiers.Length; i++)
            {
                var element = modifiersProp.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("_statType").enumValueIndex = (int)modifiers[i].stat;
                element.FindPropertyRelative("_modifierType").enumValueIndex = (int)modifiers[i].type;
                element.FindPropertyRelative("_magnitude").floatValue = modifiers[i].magnitude;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, $"{OutputFolder}/{assetName}.asset");
        }

        private static void CreateSkillTreeResetItem(
            string assetName, string storageableId, string displayName, string description,
            ItemRarity rarity, SkillTreeResetScope scope, string targetCharacterId)
        {
            var asset = ScriptableObject.CreateInstance<SkillTreeResetItem>();
            var so = new SerializedObject(asset);

            so.FindProperty("_storageableId").stringValue = storageableId;
            so.FindProperty("_displayName").stringValue = displayName;
            so.FindProperty("_description").stringValue = description;
            so.FindProperty("_rarity").enumValueIndex = (int)rarity;

            SetDefaultStackableStrategy(so);

            so.FindProperty("_scope").enumValueIndex = (int)scope;
            so.FindProperty("_targetCharacterId").stringValue = targetCharacterId;

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(asset, $"{OutputFolder}/{assetName}.asset");
        }

        /// <summary>
        /// Assigns a default StackableStorageStrategy via managed reference,
        /// since ItemDefinition's [SerializeReference] field has no concrete
        /// type until one is explicitly set — an asset created purely via
        /// ScriptableObject.CreateInstance would otherwise have a null strategy.
        /// </summary>
        private static void SetDefaultStackableStrategy(SerializedObject so)
        {
            so.FindProperty("_storageStrategy").managedReferenceValue = new StackableStorageStrategy();
        }

        private static void EnsureFolderExists()
        {
            if (AssetDatabase.IsValidFolder(OutputFolder)) return;

            if (!AssetDatabase.IsValidFolder("Assets/_TestData"))
                AssetDatabase.CreateFolder("Assets", "_TestData");

            AssetDatabase.CreateFolder("Assets/_TestData", "CharacterStats");
        }
    }
}
#endif