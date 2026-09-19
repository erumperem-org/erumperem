#if UNITY_EDITOR
using Erumperem.Combat.Authoring;
using Game.Core.Domain;
using UnityEditor;
using UnityEngine;

namespace Erumperem.Editor.Combat
{
    [CustomEditor(typeof(CombatAbilityAsset))]
    public sealed class CombatAbilityAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawIdentity();

            var abilityKind = (CombatAbilityKind)serializedObject.FindProperty("_abilityKind").enumValueIndex;
            var placement = (CombatAbilityPlacement)serializedObject.FindProperty("_placement").enumValueIndex;

            if (abilityKind == CombatAbilityKind.Active)
            {
                DrawActiveSkill();
            }
            else
            {
                DrawPassive();
            }

            if (placement == CombatAbilityPlacement.TreeNode)
            {
                DrawTreeNode();
            }

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawIdentity()
        {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_abilityId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_displayName"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_ownerCharacterId"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_abilityKind"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_placement"));

            var placement = (CombatAbilityPlacement)serializedObject.FindProperty("_placement").enumValueIndex;
            if (placement == CombatAbilityPlacement.TreeNode)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_treeIndex"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_tierIndex"));
                var abilityKind = (CombatAbilityKind)serializedObject.FindProperty("_abilityKind").enumValueIndex;
                if (abilityKind == CombatAbilityKind.Passive)
                {
                    EditorGUILayout.PropertyField(serializedObject.FindProperty("_passiveIndex"));
                }
            }

            if (placement == CombatAbilityPlacement.CorruptionPassive)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_corruptionMinTier"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_designerNotes"));
        }

        private void DrawActiveSkill()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Active skill", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_activeSkillTypeLabel"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_activeSkillDamageElement"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_baseDamageMinimum"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_baseDamageMaximum"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_baseCriticalHitChanceFraction"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_baseHitAccuracyFraction"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_targetSelectionKind"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_aiAbsoluteChanceToConsiderWhenEligible"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_aiOnlyEligibleWhenOwnHpFractionBelow"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_corruptionCostAddedWhenPlayerCasts"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hitCount"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_chanceToNotEndTurn"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_followUpSkillIds"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_grantsBonusActionsToAllies"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_accuracyPenaltyPerLivingEnemy"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_hasBonusDamagePerOwnToken"));
            if (serializedObject.FindProperty("_hasBonusDamagePerOwnToken").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_bonusDamagePerOwnToken"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_bonusDamagePerOwnTokenStacks"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_computeFromDebuffTypesOnTarget"));
            if (serializedObject.FindProperty("_computeFromDebuffTypesOnTarget").boolValue)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_damagePerDistinctDebuffType"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_critChancePerDistinctDebuffType"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("_accuracyPerDistinctDebuffType"));
            }

            EditorGUILayout.PropertyField(serializedObject.FindProperty("_canTargetDeadAllies"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_effectsAppliedAfterSuccessfulHit"));
        }

        private void DrawPassive()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Passive", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_requiredPartyRole"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_chanceToTrigger"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxTriggersPerBattle"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_maxTriggersPerTurn"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_passiveConditions"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_passiveEffects"));
        }

        private void DrawTreeNode()
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Skill tree node", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_treeNodeUnlockCost"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_treeNodePrerequisiteAbilityIds"));
        }
    }
}
#endif
