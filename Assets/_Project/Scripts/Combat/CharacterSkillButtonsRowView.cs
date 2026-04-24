using System;
using System.Linq;
using DG.Tweening;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Uma linha (prefab <c>CharacterSkillButtonsHorizontalContainer</c>) com até 7 instâncias de
    /// <c>SkillButtonPanel</c>, reutilizável por combatente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterSkillButtonsRowView : MonoBehaviour
    {
        private const int MaxSlots = 7;
        private const float RowIntroScaleFrom = 0.94f;
        private const float RowIntroDuration = 0.2f;

        private readonly CharacterSkillButtonSlot[] _slots = new CharacterSkillButtonSlot[MaxSlots];
        private string _combatantId;
        private CombatSkillButtonBarUIManager _barManager;
        private GameObject _slotPrefab;
        private RectTransform _rowRect;

        public string CombatantId => _combatantId;

        public void Build(
            CombatSkillButtonBarUIManager barManager,
            string combatantId,
            GameObject skillButtonPanelPrefab)
        {
            _barManager = barManager;
            _combatantId = combatantId;
            _slotPrefab = skillButtonPanelPrefab;
            for (var childIndex = transform.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = transform.GetChild(childIndex);
                if (child != null)
                {
                    Destroy(child.gameObject);
                }
            }

            for (var slotIndex = 0; slotIndex < MaxSlots; slotIndex++)
            {
                var go = Instantiate(_slotPrefab, transform, false);
                var slot = go.GetComponent<CharacterSkillButtonSlot>() ?? go.AddComponent<CharacterSkillButtonSlot>();
                var indexCopy = slotIndex;
                slot.Wire(() => _barManager.NotifySkillBarSlotSelected(_combatantId, indexCopy));
                _slots[slotIndex] = slot;
            }
        }

        public void DismissOtherDescriptionPanels(CharacterSkillButtonSlot openSlot)
        {
            for (var slotIndex = 0; slotIndex < MaxSlots; slotIndex++)
            {
                var slot = _slots[slotIndex];
                if (slot == null || slot == openSlot)
                {
                    continue;
                }

                slot.DismissDescriptionFromSiblings();
            }
        }

        private void OnEnable()
        {
            if (_rowRect == null)
            {
                _rowRect = (RectTransform)transform;
            }

            if (_rowRect == null)
            {
                return;
            }

            _rowRect.DOKill(false);
            _rowRect.localScale = new Vector3(RowIntroScaleFrom, RowIntroScaleFrom, 1f);
            _rowRect
                .DOScale(Vector3.one, RowIntroDuration)
                .SetEase(Ease.OutBack)
                .SetLink(_rowRect.gameObject);
        }

        public void Refresh(
            BattleState battleState,
            BattleSimulator battleSimulator,
            Combatant subject,
            bool canThisRowIssuePlayerCommands,
            int? barSelectedZeroBased,
            string barSelectedOwnerCombatantId,
            Combatant selectedEnemyOrNull)
        {
            if (subject == null || string.IsNullOrEmpty(subject.Identity.Id))
            {
                return;
            }

            var isEnemy = subject.Position.Side == Side.Enemies;
            var skillIds = subject.SkillLoadout.Skills
                .Where(id => battleState.SkillsById.ContainsKey(id))
                .Take(MaxSlots)
                .ToList();

            for (var slotIndex = 0; slotIndex < MaxSlots; slotIndex++)
            {
                var slot = _slots[slotIndex];
                if (slotIndex >= skillIds.Count)
                {
                    slot.SetVisible(false);
                    continue;
                }

                var skillId = skillIds[slotIndex];
                if (!battleState.SkillsById.TryGetValue(skillId, out var skillDefinition))
                {
                    slot.SetVisible(false);
                    continue;
                }

                var playerLine = CombatSkillPlayerDescriptionFormatter.BuildSummaryLine(skillDefinition);
                var skillColor = SkillUiColorPalette.GetColorForSkillId(skillId);
                var interactable = !isEnemy && canThisRowIssuePlayerCommands &&
                    PlayerActionBuilder.TryCreate(
                        battleState,
                        battleSimulator,
                        subject,
                        slotIndex,
                        selectedEnemyOrNull) != null;

                var isSelected = barSelectedZeroBased.HasValue &&
                    barSelectedZeroBased.Value == slotIndex &&
                    !string.IsNullOrEmpty(barSelectedOwnerCombatantId) &&
                    string.Equals(
                        barSelectedOwnerCombatantId,
                        _combatantId,
                        StringComparison.Ordinal);
                slot.SetVisible(true);
                slot.ApplyVisuals(skillColor, interactable, isSelected, playerLine);
            }
        }
    }
}
