using System;
using System.Linq;
using Erumperem.Combat.Runtime;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Presentation;
using TMPro;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Atualiza SkillTitle e os painéis Info da HUD com stats da skill em hover ou selecionada.
    /// </summary>
    [DefaultExecutionOrder(30)]
    [DisallowMultipleComponent]
    public sealed class CombatSkillInfoPanelPresenter : MonoBehaviour
    {
        private const string TargetCountInfoObjectName = "Info";
        private const string DamageRangeInfoObjectName = "Info (1)";
        private const string CriticalChanceInfoObjectName = "Info (2)";
        private const string CorruptionCostInfoObjectName = "Info (3)";
        private const string SkillTitleObjectName = "SkillTitle";
        private const string SkillsSectionObjectName = "Skills";

        [SerializeField] private CombatSkillButtonBarUIManager skillButtonBarUIManager;
        [SerializeField] private TextMeshProUGUI skillTitleLabel;
        [SerializeField] private TextMeshProUGUI targetCountLabel;
        [SerializeField] private TextMeshProUGUI damageRangeLabel;
        [SerializeField] private TextMeshProUGUI criticalChanceLabel;
        [SerializeField] private TextMeshProUGUI corruptionCostLabel;

        private CombatSessionHub _sessionHub;
        private readonly CombatSessionHubSubscription _sessionHubSubscription = new();
        private CombatPrototypeController _combatSession;
        private CharacterSkillButtonsRowView _skillsRowView;
        private string _lastRenderedDisplaySignature = string.Empty;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            _sessionHubSubscription.Subscribe(_sessionHub, HandleCombatSessionReadyForUi, HandleCombatSessionClosed);
            _sessionHubSubscription.TryCatchUpWithActiveCombatSession(_combatSession);
        }

        private void OnDisable()
        {
            _sessionHubSubscription.Unsubscribe();
        }

        private void LateUpdate()
        {
            if (_skillsRowView == null && skillButtonBarUIManager != null)
            {
                _skillsRowView = skillButtonBarUIManager.SkillsRowView;
            }

            RefreshSkillInfoPanel();
        }

        private void HandleCombatSessionReadyForUi(CombatPrototypeController combatSession)
        {
            _combatSession = combatSession;
            _skillsRowView = skillButtonBarUIManager != null
                ? skillButtonBarUIManager.SkillsRowView
                : null;
        }

        private void HandleCombatSessionClosed()
        {
            _combatSession = null;
            _skillsRowView = null;
            ClearSkillInfoPanel();
        }

        private void ResolveReferences()
        {
            if (_sessionHub == null)
            {
                _sessionHub = FindFirstObjectByType<CombatSessionHub>();
            }

            if (skillButtonBarUIManager == null)
            {
                skillButtonBarUIManager = FindFirstObjectByType<CombatSkillButtonBarUIManager>();
            }

            if (_skillsRowView == null && skillButtonBarUIManager != null)
            {
                _skillsRowView = skillButtonBarUIManager.SkillsRowView;
            }

            if (skillTitleLabel == null)
            {
                skillTitleLabel = FindLabelInSkillsSection(SkillTitleObjectName);
            }

            if (targetCountLabel == null)
            {
                targetCountLabel = FindInfoLabel(TargetCountInfoObjectName);
            }

            if (damageRangeLabel == null)
            {
                damageRangeLabel = FindInfoLabel(DamageRangeInfoObjectName);
            }

            if (criticalChanceLabel == null)
            {
                criticalChanceLabel = FindInfoLabel(CriticalChanceInfoObjectName);
            }

            if (corruptionCostLabel == null)
            {
                corruptionCostLabel = FindInfoLabel(CorruptionCostInfoObjectName);
            }
        }

        private TextMeshProUGUI FindInfoLabel(string infoObjectName)
        {
            var infoTransform = FindChildTransformRecursive(transform, infoObjectName);
            return infoTransform != null
                ? infoTransform.GetComponentInChildren<TextMeshProUGUI>(true)
                : null;
        }

        private TextMeshProUGUI FindLabelInSkillsSection(string objectName)
        {
            var skillsSectionRoot = FindSkillsSectionRoot();
            if (skillsSectionRoot == null)
            {
                return null;
            }

            var labelTransform = FindChildTransformRecursive(skillsSectionRoot, objectName);
            return labelTransform != null
                ? labelTransform.GetComponent<TextMeshProUGUI>()
                : null;
        }

        private Transform FindSkillsSectionRoot()
        {
            var cursor = transform;
            while (cursor != null)
            {
                if (string.Equals(cursor.name, SkillsSectionObjectName, StringComparison.Ordinal))
                {
                    return cursor;
                }

                cursor = cursor.parent;
            }

            return null;
        }

        private static Transform FindChildTransformRecursive(Transform parentTransform, string objectName)
        {
            for (var childIndex = 0; childIndex < parentTransform.childCount; childIndex++)
            {
                var childTransform = parentTransform.GetChild(childIndex);
                if (string.Equals(childTransform.name, objectName, StringComparison.Ordinal))
                {
                    return childTransform;
                }

                var nestedMatch = FindChildTransformRecursive(childTransform, objectName);
                if (nestedMatch != null)
                {
                    return nestedMatch;
                }
            }

            return null;
        }

        private void RefreshSkillInfoPanel()
        {
            if (_combatSession == null || _skillsRowView == null || !_combatSession.IsBattleOngoing)
            {
                ClearSkillInfoPanel();
                return;
            }

            if (!TryResolveDisplayedSkillSlot(
                    out var ownerCombatantId,
                    out var zeroBasedSlotIndex,
                    out var actingCombatant,
                    out var skillDefinition))
            {
                ClearSkillInfoPanel();
                return;
            }

            var previewTarget = ResolvePreviewTarget(actingCombatant, skillDefinition, zeroBasedSlotIndex);
            var displaySignature = BuildDisplaySignature(
                ownerCombatantId,
                zeroBasedSlotIndex,
                previewTarget?.Identity.Id,
                _combatSession.BattleState.CorruptionTier);

            if (string.Equals(_lastRenderedDisplaySignature, displaySignature, StringComparison.Ordinal))
            {
                return;
            }

            _lastRenderedDisplaySignature = displaySignature;

            var skillStats = SkillCombatHudStatsBuilder.Build(
                _combatSession.BattleState,
                actingCombatant,
                skillDefinition,
                previewTarget);

            if (skillTitleLabel != null)
            {
                skillTitleLabel.text = skillDefinition.Name;
            }

            if (targetCountLabel != null)
            {
                targetCountLabel.text = CombatSkillHudInfoFormatter.FormatTargetCountLine(skillStats.TargetCount);
            }

            if (damageRangeLabel != null)
            {
                damageRangeLabel.text = CombatSkillHudInfoFormatter.FormatDamageRangeLine(skillStats);
            }

            if (criticalChanceLabel != null)
            {
                criticalChanceLabel.text = CombatSkillHudInfoFormatter.FormatCriticalChanceLine(
                    skillStats.CriticalChanceFraction);
            }

            if (corruptionCostLabel != null)
            {
                corruptionCostLabel.text = CombatSkillHudInfoFormatter.FormatCorruptionCostLine(
                    skillStats.CorruptionCost);
            }
        }

        private bool TryResolveDisplayedSkillSlot(
            out string ownerCombatantId,
            out int zeroBasedSlotIndex,
            out Combatant actingCombatant,
            out SkillDefinition skillDefinition)
        {
            ownerCombatantId = string.Empty;
            zeroBasedSlotIndex = default;
            actingCombatant = null;
            skillDefinition = null;

            var battleState = _combatSession.BattleState;
            if (battleState == null)
            {
                return false;
            }

            ownerCombatantId = _skillsRowView.CombatantId;
            if (string.IsNullOrEmpty(ownerCombatantId))
            {
                return false;
            }

            actingCombatant = _combatSession.FindCombatantById(ownerCombatantId);
            if (actingCombatant == null || actingCombatant.Health.IsDead)
            {
                return false;
            }

            _combatSession.GetSkillBarSelection(out var selectedSlot, out var selectedOwnerCombatantId);
            var hasLockedSelection = selectedSlot.HasValue &&
                string.Equals(selectedOwnerCombatantId, ownerCombatantId, StringComparison.Ordinal);

            if (hasLockedSelection)
            {
                zeroBasedSlotIndex = selectedSlot.Value;
            }
            else if (_skillsRowView.HoveredZeroBasedSlotIndex.HasValue)
            {
                zeroBasedSlotIndex = _skillsRowView.HoveredZeroBasedSlotIndex.Value;
            }
            else
            {
                return false;
            }

            var skillIds = actingCombatant.SkillLoadout.Skills
                .Where(skillId => battleState.SkillsById.ContainsKey(skillId))
                .Take(CharacterSkillButtonsRowView.MaxVisibleSlots)
                .ToList();

            if (zeroBasedSlotIndex < 0 || zeroBasedSlotIndex >= skillIds.Count)
            {
                return false;
            }

            return battleState.SkillsById.TryGetValue(skillIds[zeroBasedSlotIndex], out skillDefinition);
        }

        private Combatant ResolvePreviewTarget(
            Combatant actingCombatant,
            SkillDefinition skillDefinition,
            int zeroBasedSlotIndex)
        {
            var battleState = _combatSession.BattleState;

            if (skillButtonBarUIManager != null &&
                skillButtonBarUIManager.TryGetHoveredLivingCombatant(out var hoveredCombatant) &&
                PlayerActionBuilder.TryCreate(
                    battleState,
                    _combatSession.BattleSimulator,
                    actingCombatant,
                    zeroBasedSlotIndex,
                    hoveredCombatant) != null)
            {
                return hoveredCombatant;
            }

            switch (skillDefinition.TargetKind)
            {
                case SkillTargetKind.Self:
                    return actingCombatant;

                case SkillTargetKind.Ally:
                    var allyTarget = battleState.Allies.FirstOrDefault(allyCandidate =>
                        !allyCandidate.Health.IsDead &&
                        PlayerActionBuilder.TryCreate(
                            battleState,
                            _combatSession.BattleSimulator,
                            actingCombatant,
                            zeroBasedSlotIndex,
                            allyCandidate) != null);
                    return allyTarget ?? actingCombatant;

                case SkillTargetKind.Enemy:
                default:
                    var selectedEnemy = _combatSession.CurrentSelectedEnemy;
                    if (selectedEnemy != null &&
                        !selectedEnemy.Health.IsDead &&
                        PlayerActionBuilder.TryCreate(
                            battleState,
                            _combatSession.BattleSimulator,
                            actingCombatant,
                            zeroBasedSlotIndex,
                            selectedEnemy) != null)
                    {
                        return selectedEnemy;
                    }

                    return battleState.Enemies.FirstOrDefault(enemyCandidate =>
                        !enemyCandidate.Health.IsDead &&
                        PlayerActionBuilder.TryCreate(
                            battleState,
                            _combatSession.BattleSimulator,
                            actingCombatant,
                            zeroBasedSlotIndex,
                            enemyCandidate) != null);
            }
        }

        private static string BuildDisplaySignature(
            string ownerCombatantId,
            int zeroBasedSlotIndex,
            string previewTargetCombatantId,
            int corruptionTier) =>
            $"{ownerCombatantId}|{zeroBasedSlotIndex}|{previewTargetCombatantId}|{corruptionTier}";

        private void ClearSkillInfoPanel()
        {
            _lastRenderedDisplaySignature = string.Empty;

            if (skillTitleLabel != null)
            {
                skillTitleLabel.text = string.Empty;
            }

            if (targetCountLabel != null)
            {
                targetCountLabel.text = "—\nTGT";
            }

            if (damageRangeLabel != null)
            {
                damageRangeLabel.text = "—\nDMG";
            }

            if (criticalChanceLabel != null)
            {
                criticalChanceLabel.text = "—\nCRT";
            }

            if (corruptionCostLabel != null)
            {
                corruptionCostLabel.text = "—\nCORR";
            }
        }
    }
}

