#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Core.Data;
using Game.Core.Models;
using Game.Core.Presentation;
using UnityEditor;
using UnityEngine;
using Erumperem.Progression;
using Erumperem.UI;

namespace Erumperem.Editor.Progression
{
    /// <summary>
    /// Gera um <see cref="SkillTreeNodeAsset"/> por nó em skill_trees.json (dados passivos + activos a partir dos JSON).
    /// Re-executar mantém conteúdo já preenchido nos campos UI quando não vazio.
    /// </summary>
    public static class SkillTreePassiveAssetGenerator
    {
        private const string ResourcesSkillNodes = "Assets/_Project/Resources/SkillTreeNodes";

        [MenuItem("Erumperem/Generate Skill Tree + Passive Assets From JSON")]
        public static void GenerateAll()
        {
            var projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            var treesPath = Path.Combine(projectRoot, "Assets", "StreamingAssets", "Data", "skill_trees.json");
            var skillsPath = Path.Combine(projectRoot, "Assets", "StreamingAssets", "Data", "skills.json");
            var passivesPath = Path.Combine(projectRoot, "Assets", "StreamingAssets", "Data", "passives.json");

            if (!File.Exists(treesPath) || !File.Exists(skillsPath) || !File.Exists(passivesPath))
            {
                Debug.LogError(
                    "Generate: faltam ficheiros em Assets/StreamingAssets/Data (skill_trees, skills, passives). " +
                    "Run Erumperem/Combat/Export Catalog first.");
                return;
            }

            Directory.CreateDirectory(ResourcesSkillNodes);

            var skills = CombatDataLoader.LoadSkills(skillsPath);
            var skillsById = skills.ToDictionary(skill => skill.Id, skill => skill);
            var passiveDefs = CombatDataLoader.LoadPassives(passivesPath);
            var passiveById = passiveDefs.ToDictionary(passive => passive.Id, passive => passive);
            var skillTrees = CombatDataLoader.LoadSkillTrees(treesPath);

            var existingNodeAssetsById = LoadExistingNodeAssetsById();

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var character in skillTrees)
                {
                    foreach (var tree in character.Trees)
                    {
                        foreach (var tier in tree.Tiers)
                        {
                            foreach (var node in tier.Nodes)
                            {
                                var nodeAsset = ResolveOrCreateNodeAsset(node.Id, existingNodeAssetsById);
                                var isPassive = string.Equals(node.Type, "Passive", System.StringComparison.OrdinalIgnoreCase);
                                var nodeSo = new SerializedObject(nodeAsset);

                                nodeSo.FindProperty("_nodeId").stringValue = node.Id;
                                nodeSo.FindProperty("_skillTreeElementCategory").enumValueIndex = (int)tree.Element;
                                nodeSo.FindProperty("_isPassiveNode").boolValue = isPassive;

                                if (isPassive && passiveById.TryGetValue(node.Id, out var passiveDefinition))
                                {
                                    PopulatePassiveFields(nodeSo, passiveDefinition);
                                    OverwriteString(
                                        nodeSo.FindProperty("_displayName"),
                                        BuildPassiveDisplayName(passiveDefinition, skillsById));
                                    OverwriteString(
                                        nodeSo.FindProperty("_descriptionForUi"),
                                        PlayerFacingText.DescribePassiveDefinitionInDetail(passiveDefinition));
                                    ClearActiveFieldsForPassive(nodeSo);
                                }
                                else if (!isPassive && skillsById.TryGetValue(node.Id, out var skillDefinition))
                                {
                                    PopulateActiveFields(nodeSo, skillDefinition);
                                    OverwriteString(nodeSo.FindProperty("_displayName"), skillDefinition.Name);
                                    OverwriteString(
                                        nodeSo.FindProperty("_descriptionForUi"),
                                        SkillPlayerDescriptionBuilder.BuildSummaryLine(skillDefinition));
                                    ClearPassiveFieldsForActive(nodeSo);
                                }
                                else
                                {
                                    Debug.LogWarning($"Generate: nó '{node.Id}' sem entrada em passives.json ou skills.json.");
                                }

                                nodeSo.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            Debug.Log(
                $"Erumperem: gerados/atualizados {nameof(SkillTreeNodeAsset)} em {ResourcesSkillNodes}. " +
                "Cada botão da árvore referencia um destes SOs.");
        }

        private static void ClearActiveFieldsForPassive(SerializedObject nodeSo)
        {
            nodeSo.FindProperty("_effectsAppliedAfterSuccessfulHit").ClearArray();
            nodeSo.FindProperty("_baseDamageMinimum").intValue = 0;
            nodeSo.FindProperty("_baseDamageMaximum").intValue = 0;
            nodeSo.FindProperty("_baseCriticalHitChanceFraction").doubleValue = 0;
            nodeSo.FindProperty("_baseHitAccuracyFraction").doubleValue = 1;
        }

        private static void ClearPassiveFieldsForActive(SerializedObject nodeSo)
        {
            nodeSo.FindProperty("_passiveDamageBonusOrIncomingMultiplierMagnitude").doubleValue = 0;
            nodeSo.FindProperty("_passiveDamageBonusFractionPerDotStackOnTarget").doubleValue = 0;
            nodeSo.FindProperty("_passiveDamageBonusFractionMaximumCap").doubleValue = 0;
            nodeSo.FindProperty("_passiveActivatesWhenHpFractionBelow").doubleValue = 0;
            nodeSo.FindProperty("_passiveStacksOrDotPotencyOrTurnsBonusInteger").intValue = 0;
            nodeSo.FindProperty("_passiveDotDurationOrMaxTurnCapInteger").intValue = 0;
            nodeSo.FindProperty("_passiveAppliesWhenSkillIdMatches").stringValue = string.Empty;
            nodeSo.FindProperty("_passivePrerequisiteSkillIdThatMustBeUsedFirst").stringValue = string.Empty;
            nodeSo.FindProperty("_passiveUsesDotTypeFilter").boolValue = false;
            nodeSo.FindProperty("_passiveUsesTokenTypeFilter").boolValue = false;
            nodeSo.FindProperty("_passiveGrantsExtraTokenOfType").boolValue = false;
            nodeSo.FindProperty("_passiveOnlyAppliesWhenActorHasTokenType").boolValue = false;
            nodeSo.FindProperty("_passiveOnlyAppliesWhenActorLacksTokenType").boolValue = false;
        }

        private static void PopulatePassiveFields(SerializedObject nodeSo, PassiveDefinition passiveDefinition)
        {
            nodeSo.FindProperty("_passiveEffectKind").enumValueIndex = (int)passiveDefinition.EffectKind;
            nodeSo.FindProperty("_passiveAppliesWhenSkillIdMatches").stringValue =
                passiveDefinition.SkillId ?? string.Empty;
            nodeSo.FindProperty("_passivePrerequisiteSkillIdThatMustBeUsedFirst").stringValue =
                passiveDefinition.PrerequisiteSkillId ?? string.Empty;

            SetOptionalEnum(
                nodeSo,
                "_passiveUsesDotTypeFilter",
                "_passiveDotTypeFilter",
                (int?)passiveDefinition.DotType);
            SetOptionalEnum(
                nodeSo,
                "_passiveUsesTokenTypeFilter",
                "_passiveTokenTypeFilter",
                (int?)passiveDefinition.TokenType);
            SetOptionalEnum(
                nodeSo,
                "_passiveGrantsExtraTokenOfType",
                "_passiveTokenTypeToGrantWhenTriggered",
                (int?)passiveDefinition.GrantTokenType);
            SetOptionalEnum(
                nodeSo,
                "_passiveOnlyAppliesWhenActorHasTokenType",
                "_passiveRequiredTokenTypeOnActor",
                (int?)passiveDefinition.IfHasTokenType);
            SetOptionalEnum(
                nodeSo,
                "_passiveOnlyAppliesWhenActorLacksTokenType",
                "_passiveBlockingTokenTypeOnActor",
                (int?)passiveDefinition.UnlessHasTokenType);

            nodeSo.FindProperty("_passiveDamageBonusOrIncomingMultiplierMagnitude").doubleValue =
                passiveDefinition.Additive;
            nodeSo.FindProperty("_passiveDamageBonusFractionPerDotStackOnTarget").doubleValue =
                passiveDefinition.AdditivePerStack;
            nodeSo.FindProperty("_passiveDamageBonusFractionMaximumCap").doubleValue = passiveDefinition.Cap;
            nodeSo.FindProperty("_passiveActivatesWhenHpFractionBelow").doubleValue = passiveDefinition.HpBelowPercent;
            nodeSo.FindProperty("_passiveStacksOrDotPotencyOrTurnsBonusInteger").intValue = passiveDefinition.IntValue;
            nodeSo.FindProperty("_passiveDotDurationOrMaxTurnCapInteger").intValue = passiveDefinition.IntValue2;
        }

        private static void PopulateActiveFields(SerializedObject nodeSo, SkillDefinition skillDefinition)
        {
            nodeSo.FindProperty("_activeSkillTypeLabel").stringValue = skillDefinition.Type ?? "Active";
            nodeSo.FindProperty("_activeSkillDamageElement").enumValueIndex = (int)skillDefinition.Element;
            nodeSo.FindProperty("_baseDamageMinimum").intValue = skillDefinition.BaseDamage.Min;
            nodeSo.FindProperty("_baseDamageMaximum").intValue = skillDefinition.BaseDamage.Max;
            nodeSo.FindProperty("_baseCriticalHitChanceFraction").doubleValue = skillDefinition.BaseCritChance;
            nodeSo.FindProperty("_baseHitAccuracyFraction").doubleValue = skillDefinition.Accuracy;
            nodeSo.FindProperty("_targetSelectionKind").enumValueIndex = (int)skillDefinition.TargetKind;
            nodeSo.FindProperty("_aiAbsoluteChanceToConsiderWhenEligible").doubleValue = skillDefinition.ChanceToUse;
            nodeSo.FindProperty("_aiOnlyEligibleWhenOwnHpFractionBelow").doubleValue = skillDefinition.SelfHpPercentBelow;
            nodeSo.FindProperty("_corruptionCostAddedWhenPlayerCasts").doubleValue = skillDefinition.CorruptionCost;

            WriteEffectList(nodeSo.FindProperty("_effectsAppliedAfterSuccessfulHit"), skillDefinition.EffectsOnHit);
        }

        private static void WriteEffectList(SerializedProperty listProperty, IReadOnlyList<EffectSpec> specs)
        {
            listProperty.ClearArray();
            if (specs == null)
            {
                return;
            }

            for (var index = 0; index < specs.Count; index++)
            {
                listProperty.InsertArrayElementAtIndex(index);
                var elementProperty = listProperty.GetArrayElementAtIndex(index);
                var specification = specs[index];
                elementProperty.FindPropertyRelative("Type").enumValueIndex = (int)specification.Type;
                elementProperty.FindPropertyRelative("Chance").doubleValue = specification.Chance;
                elementProperty.FindPropertyRelative("Stacks").intValue = specification.Stacks;
                elementProperty.FindPropertyRelative("Potency").intValue = specification.Potency;
                elementProperty.FindPropertyRelative("Duration").intValue = specification.Duration;
                elementProperty.FindPropertyRelative("Steps").intValue = specification.Steps;
                elementProperty.FindPropertyRelative("EffectScopeName").stringValue =
                    specification.EffectScope.ToString();
                elementProperty.FindPropertyRelative("UseToken").boolValue = specification.Token.HasValue;
                if (specification.Token.HasValue)
                {
                    elementProperty.FindPropertyRelative("Token").enumValueIndex = (int)specification.Token.Value;
                }

                elementProperty.FindPropertyRelative("UseDot").boolValue = specification.Dot.HasValue;
                if (specification.Dot.HasValue)
                {
                    elementProperty.FindPropertyRelative("Dot").enumValueIndex = (int)specification.Dot.Value;
                }
            }
        }

        private static void SetOptionalEnum(SerializedObject so, string boolFieldName, string enumFieldName, int? value)
        {
            so.FindProperty(boolFieldName).boolValue = value.HasValue;
            if (value.HasValue)
            {
                so.FindProperty(enumFieldName).enumValueIndex = value.Value;
            }
        }

        private static Dictionary<string, SkillTreeNodeAsset> LoadExistingNodeAssetsById()
        {
            var assetsById = new Dictionary<string, SkillTreeNodeAsset>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var assetGuid in AssetDatabase.FindAssets("t:SkillTreeNodeAsset", new[] { ResourcesSkillNodes }))
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                var nodeAsset = AssetDatabase.LoadAssetAtPath<SkillTreeNodeAsset>(assetPath);
                if (nodeAsset == null || string.IsNullOrWhiteSpace(nodeAsset.NodeId))
                {
                    continue;
                }

                assetsById[nodeAsset.NodeId] = nodeAsset;
            }

            return assetsById;
        }

        private static SkillTreeNodeAsset ResolveOrCreateNodeAsset(
            string nodeId,
            Dictionary<string, SkillTreeNodeAsset> existingNodeAssetsById)
        {
            if (existingNodeAssetsById.TryGetValue(nodeId, out var existingAsset) && existingAsset != null)
            {
                return existingAsset;
            }

            var nodePath = $"{ResourcesSkillNodes}/{nodeId}.asset";
            var nodeAsset = AssetDatabase.LoadAssetAtPath<SkillTreeNodeAsset>(nodePath);
            if (nodeAsset == null)
            {
                nodeAsset = ScriptableObject.CreateInstance<SkillTreeNodeAsset>();
                AssetDatabase.CreateAsset(nodeAsset, nodePath);
            }

            existingNodeAssetsById[nodeId] = nodeAsset;
            return nodeAsset;
        }

        private static string BuildPassiveDisplayName(
            PassiveDefinition passiveDefinition,
            Dictionary<string, SkillDefinition> skillsById)
        {
            var summary = PassivePlayerDescriptionBuilder.BuildSummaryLine(passiveDefinition, skillsById);
            if (string.IsNullOrWhiteSpace(summary))
            {
                return passiveDefinition.Id;
            }

            return summary.TrimEnd('.');
        }

        private static void OverwriteString(SerializedProperty stringProperty, string newValue)
        {
            stringProperty.stringValue = newValue ?? string.Empty;
        }
    }
}
#endif
