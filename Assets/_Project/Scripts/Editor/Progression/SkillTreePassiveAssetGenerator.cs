#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.Core.Data;
using Game.Core.Models;
using UnityEditor;
using UnityEngine;
using Erumperem.Progression;

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
            var treesPath = Path.Combine(projectRoot, "Game.Simulations", "Data", "skill_trees.json");
            var skillsPath = Path.Combine(projectRoot, "Game.Simulations", "Data", "skills.json");
            var passivesPath = Path.Combine(projectRoot, "Game.Simulations", "Data", "passives.json");

            if (!File.Exists(treesPath) || !File.Exists(skillsPath) || !File.Exists(passivesPath))
            {
                Debug.LogError(
                    "Generate: faltam ficheiros em Game.Simulations/Data (skill_trees, skills, passives).");
                return;
            }

            Directory.CreateDirectory(ResourcesSkillNodes);

            var skills = CombatDataLoader.LoadSkills(skillsPath);
            var skillsById = skills.ToDictionary(skill => skill.Id, skill => skill);
            var passiveDefs = CombatDataLoader.LoadPassives(passivesPath);
            var passiveById = passiveDefs.ToDictionary(passive => passive.Id, passive => passive);
            var skillTrees = CombatDataLoader.LoadSkillTrees(treesPath);

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
                                var nodePath = $"{ResourcesSkillNodes}/{node.Id}.asset";
                                var nodeAsset = AssetDatabase.LoadAssetAtPath<SkillTreeNodeAsset>(nodePath);
                                if (nodeAsset == null)
                                {
                                    nodeAsset = ScriptableObject.CreateInstance<SkillTreeNodeAsset>();
                                    AssetDatabase.CreateAsset(nodeAsset, nodePath);
                                }

                                var isPassive = string.Equals(node.Type, "Passive", System.StringComparison.OrdinalIgnoreCase);
                                var nodeSo = new SerializedObject(nodeAsset);

                                nodeSo.FindProperty("_nodeId").stringValue = node.Id;
                                nodeSo.FindProperty("_treeElement").enumValueIndex = (int)tree.Element;
                                nodeSo.FindProperty("_isPassiveNode").boolValue = isPassive;

                                if (isPassive && passiveById.TryGetValue(node.Id, out var passiveDefinition))
                                {
                                    PopulatePassiveFields(nodeSo, passiveDefinition);
                                    FillIfEmpty(nodeSo.FindProperty("_displayName"), passiveDefinition.Id);
                                    FillIfEmpty(
                                        nodeSo.FindProperty("_descriptionForUi"),
                                        BuildPassiveSummary(passiveDefinition));
                                    ClearActiveFieldsForPassive(nodeSo);
                                }
                                else if (!isPassive && skillsById.TryGetValue(node.Id, out var skillDefinition))
                                {
                                    PopulateActiveFields(nodeSo, skillDefinition);
                                    FillIfEmpty(nodeSo.FindProperty("_displayName"), skillDefinition.Name);
                                    FillIfEmpty(
                                        nodeSo.FindProperty("_descriptionForUi"),
                                        $"{skillDefinition.Type} ({skillDefinition.Element}) — dano {skillDefinition.BaseDamage.Min}-{skillDefinition.BaseDamage.Max}.");
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
            nodeSo.FindProperty("_effectsOnHit").ClearArray();
            nodeSo.FindProperty("_comboBonus").ClearArray();
            nodeSo.FindProperty("_damageMin").intValue = 0;
            nodeSo.FindProperty("_damageMax").intValue = 0;
            nodeSo.FindProperty("_baseCritChance").doubleValue = 0;
            nodeSo.FindProperty("_accuracy").doubleValue = 1;
            nodeSo.FindProperty("_weight").intValue = 1;
        }

        private static void ClearPassiveFieldsForActive(SerializedObject nodeSo)
        {
            nodeSo.FindProperty("_passiveAdditive").doubleValue = 0;
            nodeSo.FindProperty("_passiveAdditivePerStack").doubleValue = 0;
            nodeSo.FindProperty("_passiveCap").doubleValue = 0;
            nodeSo.FindProperty("_passiveHpBelowPercent").doubleValue = 0;
            nodeSo.FindProperty("_passiveIntValue").intValue = 0;
            nodeSo.FindProperty("_passiveIntValue2").intValue = 0;
            nodeSo.FindProperty("_passiveSkillId").stringValue = string.Empty;
            nodeSo.FindProperty("_passivePrerequisiteSkillId").stringValue = string.Empty;
            nodeSo.FindProperty("_passiveUseDotType").boolValue = false;
            nodeSo.FindProperty("_passiveUseTokenType").boolValue = false;
            nodeSo.FindProperty("_passiveUseGrantTokenType").boolValue = false;
            nodeSo.FindProperty("_passiveUseIfHasTokenType").boolValue = false;
            nodeSo.FindProperty("_passiveUseUnlessHasTokenType").boolValue = false;
        }

        private static void PopulatePassiveFields(SerializedObject nodeSo, PassiveDefinition passiveDefinition)
        {
            nodeSo.FindProperty("_passiveEffectKind").enumValueIndex = (int)passiveDefinition.EffectKind;
            nodeSo.FindProperty("_passiveSkillId").stringValue = passiveDefinition.SkillId ?? string.Empty;
            nodeSo.FindProperty("_passivePrerequisiteSkillId").stringValue =
                passiveDefinition.PrerequisiteSkillId ?? string.Empty;

            SetOptionalEnum(nodeSo, "_passiveUseDotType", "_passiveDotType", (int?)passiveDefinition.DotType);
            SetOptionalEnum(nodeSo, "_passiveUseTokenType", "_passiveTokenType", (int?)passiveDefinition.TokenType);
            SetOptionalEnum(
                nodeSo,
                "_passiveUseGrantTokenType",
                "_passiveGrantTokenType",
                (int?)passiveDefinition.GrantTokenType);
            SetOptionalEnum(
                nodeSo,
                "_passiveUseIfHasTokenType",
                "_passiveIfHasTokenType",
                (int?)passiveDefinition.IfHasTokenType);
            SetOptionalEnum(
                nodeSo,
                "_passiveUseUnlessHasTokenType",
                "_passiveUnlessHasTokenType",
                (int?)passiveDefinition.UnlessHasTokenType);

            nodeSo.FindProperty("_passiveAdditive").doubleValue = passiveDefinition.Additive;
            nodeSo.FindProperty("_passiveAdditivePerStack").doubleValue = passiveDefinition.AdditivePerStack;
            nodeSo.FindProperty("_passiveCap").doubleValue = passiveDefinition.Cap;
            nodeSo.FindProperty("_passiveHpBelowPercent").doubleValue = passiveDefinition.HpBelowPercent;
            nodeSo.FindProperty("_passiveIntValue").intValue = passiveDefinition.IntValue;
            nodeSo.FindProperty("_passiveIntValue2").intValue = passiveDefinition.IntValue2;
        }

        private static void PopulateActiveFields(SerializedObject nodeSo, SkillDefinition skillDefinition)
        {
            nodeSo.FindProperty("_activeSkillType").stringValue = skillDefinition.Type ?? "Active";
            nodeSo.FindProperty("_activeElement").enumValueIndex = (int)skillDefinition.Element;
            nodeSo.FindProperty("_damageMin").intValue = skillDefinition.BaseDamage.Min;
            nodeSo.FindProperty("_damageMax").intValue = skillDefinition.BaseDamage.Max;
            nodeSo.FindProperty("_baseCritChance").doubleValue = skillDefinition.BaseCritChance;
            nodeSo.FindProperty("_accuracy").doubleValue = skillDefinition.Accuracy;
            nodeSo.FindProperty("_targetKind").enumValueIndex = (int)skillDefinition.TargetKind;
            nodeSo.FindProperty("_weight").intValue = skillDefinition.Weight;
            nodeSo.FindProperty("_corruptionCost").doubleValue = skillDefinition.CorruptionCost;

            WriteEffectList(nodeSo.FindProperty("_effectsOnHit"), skillDefinition.EffectsOnHit);
            WriteEffectList(nodeSo.FindProperty("_comboBonus"), skillDefinition.ComboBonus);
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
                elementProperty.FindPropertyRelative("EffectScope").stringValue =
                    specification.EffectScope ?? "Default";
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

        private static void FillIfEmpty(SerializedProperty stringProperty, string newValue)
        {
            if (string.IsNullOrEmpty(stringProperty.stringValue))
            {
                stringProperty.stringValue = newValue ?? string.Empty;
            }
        }

        private static string BuildPassiveSummary(PassiveDefinition passiveDefinition)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"Efeito: {passiveDefinition.EffectKind}");

            if (!string.IsNullOrEmpty(passiveDefinition.SkillId))
            {
                stringBuilder.AppendLine($"Skill: {passiveDefinition.SkillId}");
            }

            if (!string.IsNullOrEmpty(passiveDefinition.PrerequisiteSkillId))
            {
                stringBuilder.AppendLine($"Pré-requisito (skill): {passiveDefinition.PrerequisiteSkillId}");
            }

            if (passiveDefinition.DotType is { } dot)
            {
                stringBuilder.AppendLine($"DOT: {dot}");
            }

            if (passiveDefinition.TokenType is { } token)
            {
                stringBuilder.AppendLine($"Token: {token}");
            }

            if (passiveDefinition.GrantTokenType is { } grantToken)
            {
                stringBuilder.AppendLine($"Concede token: {grantToken}");
            }

            if (passiveDefinition.IfHasTokenType is { } ifHasToken)
            {
                stringBuilder.AppendLine($"Se tiver token: {ifHasToken}");
            }

            if (passiveDefinition.UnlessHasTokenType is { } unlessToken)
            {
                stringBuilder.AppendLine($"A menos que tenha token: {unlessToken}");
            }

            if (passiveDefinition.Additive != 0)
            {
                stringBuilder.AppendLine($"Aditivo: {passiveDefinition.Additive}");
            }

            if (passiveDefinition.AdditivePerStack != 0)
            {
                stringBuilder.AppendLine($"Por stack: {passiveDefinition.AdditivePerStack} (cap {passiveDefinition.Cap})");
            }

            if (passiveDefinition.HpBelowPercent > 0)
            {
                stringBuilder.AppendLine($"HP abaixo de: {passiveDefinition.HpBelowPercent:P0}");
            }

            if (passiveDefinition.IntValue != 0 || passiveDefinition.IntValue2 != 0)
            {
                stringBuilder.AppendLine($"Int: {passiveDefinition.IntValue}, {passiveDefinition.IntValue2}");
            }

            return stringBuilder.ToString().TrimEnd();
        }
    }
}
#endif
