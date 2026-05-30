using System.Linq;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.HealthBars
{
    /// <summary>
    /// Mostra previsão de dano na barra de vida do inimigo sob o rato quando há uma skill de dano
    /// seleccionada na hotbar e o jogador pode executá-la contra esse alvo.
    /// </summary>
    [DefaultExecutionOrder(30)]
    public sealed class CombatSkillDamagePreviewBinder : MonoBehaviour
    {
        [SerializeField] private CombatPrototypeController combatSession;
        [SerializeField] private CombatHealthBarsBinder healthBarsBinder;
        [SerializeField] private CombatSkillButtonBarUIManager skillButtonBarUIManager;

        private string _previewCombatantIdWithActiveOverlay;

        private void Awake()
        {
            if (combatSession == null)
            {
                combatSession = FindFirstObjectByType<CombatPrototypeController>();
            }

            if (healthBarsBinder == null)
            {
                healthBarsBinder = FindFirstObjectByType<CombatHealthBarsBinder>();
            }

            if (skillButtonBarUIManager == null)
            {
                skillButtonBarUIManager = FindFirstObjectByType<CombatSkillButtonBarUIManager>();
            }
        }

        private void LateUpdate()
        {
            if (combatSession == null || healthBarsBinder == null || !combatSession.IsBattleOngoing)
            {
                ClearActivePreview();
                return;
            }

            if (!TryResolveSkillDamagePreviewContext(
                    out var actor,
                    out var hoveredEnemy,
                    out var skill,
                    out var damagePreview))
            {
                ClearActivePreview();
                return;
            }

            if (!healthBarsBinder.TryGetHealthBarHudView(hoveredEnemy.Identity.Id, out var healthBarHudView))
            {
                ClearActivePreview();
                return;
            }

            if (!string.Equals(
                    _previewCombatantIdWithActiveOverlay,
                    hoveredEnemy.Identity.Id,
                    System.StringComparison.Ordinal))
            {
                ClearActivePreview();
            }

            _previewCombatantIdWithActiveOverlay = hoveredEnemy.Identity.Id;
            healthBarHudView.SetSkillDamagePreview(
                damagePreview.MinDamageOnHit,
                damagePreview.MaxDamageOnHit,
                damagePreview.MinHpAfterHit,
                damagePreview.MaxHpAfterHit,
                damagePreview.IsGuaranteedKillOnHit,
                FormatFloatingDamageText(damagePreview));
        }

        private bool TryResolveSkillDamagePreviewContext(
            out Combatant actor,
            out Combatant hoveredEnemy,
            out SkillDefinition skill,
            out SkillDamagePreview damagePreview)
        {
            actor = null;
            hoveredEnemy = null;
            skill = null;
            damagePreview = default;

            combatSession.GetSkillBarSelection(out var selectedSlot, out var skillBarOwnerCombatantId);
            if (!selectedSlot.HasValue || string.IsNullOrEmpty(skillBarOwnerCombatantId))
            {
                return false;
            }

            actor = combatSession.FindCombatantById(skillBarOwnerCombatantId);
            if (actor == null || !combatSession.IsPlayerCommandingCombatant(actor))
            {
                return false;
            }

            var battleState = combatSession.BattleState;
            var skillIds = actor.SkillLoadout.Skills
                .Where(skillId => battleState.SkillsById.ContainsKey(skillId))
                .Take(7)
                .ToList();
            if (selectedSlot.Value < 0 || selectedSlot.Value >= skillIds.Count)
            {
                return false;
            }

            skill = battleState.SkillsById[skillIds[selectedSlot.Value]];
            if (!SkillDamagePreviewCalculator.HasDirectDamage(skill))
            {
                return false;
            }

            if (skillButtonBarUIManager == null ||
                !skillButtonBarUIManager.TryGetHoveredLivingCombatant(out hoveredEnemy))
            {
                return false;
            }

            if (hoveredEnemy.Position.Side == actor.Position.Side)
            {
                return false;
            }

            var chosenAction = PlayerActionBuilder.TryCreate(
                battleState,
                combatSession.BattleSimulator,
                actor,
                selectedSlot.Value,
                hoveredEnemy);
            if (chosenAction == null)
            {
                return false;
            }

            return SkillDamagePreviewCalculator.TryCompute(
                battleState,
                actor,
                hoveredEnemy,
                skill,
                out damagePreview);
        }

        private static string FormatFloatingDamageText(SkillDamagePreview damagePreview)
        {
            var damageRangeText = damagePreview.MinDamageOnHit == damagePreview.MaxDamageOnHit
                ? $"{damagePreview.MinDamageOnHit}"
                : $"{damagePreview.MinDamageOnHit}–{damagePreview.MaxDamageOnHit}";

            var hitChancePercent = (int)System.Math.Round(damagePreview.HitChanceFraction * 100.0);
            if (hitChancePercent < 100)
            {
                return $"{damageRangeText} ({hitChancePercent}% acerto)";
            }

            return damageRangeText;
        }

        private void ClearActivePreview()
        {
            if (string.IsNullOrEmpty(_previewCombatantIdWithActiveOverlay))
            {
                return;
            }

            if (healthBarsBinder != null &&
                healthBarsBinder.TryGetHealthBarHudView(
                    _previewCombatantIdWithActiveOverlay,
                    out var healthBarHudView))
            {
                healthBarHudView.ClearSkillDamagePreview();
            }

            _previewCombatantIdWithActiveOverlay = null;
        }

        private void OnDisable()
        {
            ClearActivePreview();
        }
    }
}
