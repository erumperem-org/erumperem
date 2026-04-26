using System;
using System.Collections.Generic;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Uma row por combatente, uma visível. Sem skill selecionada, o hover 3D mostra a row desse personagem.
    /// Com slot selecionado (clique ou 1–7), a row do dono fica travada ao hover.
    /// </summary>
    public sealed class CombatSkillButtonBarUIManager : MonoBehaviour
    {
        [SerializeField] private Transform rowsParent;
        [SerializeField] private GameObject characterSkillButtonsRowPrefab;
        [SerializeField] private GameObject skillButtonPanelPrefab;
        [SerializeField] private float worldRaycastDistance = 200f;
        [SerializeField] private CombatSkillBarSelectionController skillBarSelectionController;

        private CombatPrototypeController _controller;
        private readonly Dictionary<string, CharacterSkillButtonsRowView> _rowsByCombatantId =
            new(StringComparer.Ordinal);
        private string _activeSkillRowCombatantId;

        public void Initialize(CombatPrototypeController controller)
        {
            _controller = controller;
            if (rowsParent == null)
            {
                rowsParent = transform;
            }

            if (skillBarSelectionController == null)
            {
                skillBarSelectionController = GetComponent<CombatSkillBarSelectionController>();
            }

            skillBarSelectionController?.Bind(controller);

            if (_controller == null)
            {
                return;
            }

            var state = _controller.BattleState;
            if (state == null)
            {
                return;
            }

            foreach (var entry in _rowsByCombatantId)
            {
                if (entry.Value != null)
                {
                    Destroy(entry.Value.gameObject);
                }
            }

            _rowsByCombatantId.Clear();
            if (characterSkillButtonsRowPrefab == null || skillButtonPanelPrefab == null)
            {
                return;
            }

            foreach (var combatant in state.GetAllCombatants())
            {
                var id = combatant.Identity.Id;
                if (string.IsNullOrEmpty(id))
                {
                    continue;
                }

                var rowObject = Instantiate(characterSkillButtonsRowPrefab, rowsParent, false);
                rowObject.name = "SkillRow_" + id;
                rowObject.SetActive(false);
                var row = rowObject.GetComponent<CharacterSkillButtonsRowView>() ??
                    rowObject.AddComponent<CharacterSkillButtonsRowView>();
                row.Build(this, id, skillButtonPanelPrefab, skillBarSelectionController);
                _rowsByCombatantId[id] = row;
            }
        }

        /// <summary>Fallback se não houver <see cref="CombatSkillBarSelectionController"/> na cena.</summary>
        public void NotifySkillBarSlotSelected(string ownerCombatantId, int zeroBasedSlot)
        {
            if (skillBarSelectionController != null)
            {
                skillBarSelectionController.RequestSelectSkillSlot(ownerCombatantId, zeroBasedSlot);
                return;
            }

            if (_controller == null)
            {
                return;
            }

            _controller.TrySelectSkillBarSlot(ownerCombatantId, zeroBasedSlot);
        }

        public void OnBattleEnded()
        {
            _activeSkillRowCombatantId = null;
            HideAllRows();
        }

        public void OnSkillBarSelectionCleared()
        {
            SyncVisibleRowWithBattle();
        }

        public void SyncVisibleRowWithBattle()
        {
            TryLockActiveRowToSelection();
            if (string.IsNullOrEmpty(_activeSkillRowCombatantId) ||
                !_rowsByCombatantId.TryGetValue(_activeSkillRowCombatantId, out var row) ||
                row == null)
            {
                return;
            }

            _controller.GetSkillBarSelection(out var slot, out var owner);
            var subject = _controller.FindCombatantById(_activeSkillRowCombatantId);
            if (subject == null)
            {
                return;
            }

            var canIssue = _controller.IsPlayerCommandingCombatant(subject);
            row.Refresh(
                _controller.BattleState,
                _controller.BattleSimulator,
                subject,
                canIssue,
                slot,
                owner,
                _controller.CurrentSelectedEnemy);
        }

        /// <summary>Se houver slot da hotbar selecionado, fixa a row ao dono.</summary>
        private bool TryLockActiveRowToSelection()
        {
            if (_controller == null)
            {
                return false;
            }

            _controller.GetSkillBarSelection(out var barSlot, out var barOwner);
            if (barSlot.HasValue && !string.IsNullOrEmpty(barOwner))
            {
                _activeSkillRowCombatantId = barOwner;
                return true;
            }

            return false;
        }

        private Combatant TryRaycastHoveredLivingCombatant()
        {
            if (_controller == null)
            {
                return null;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return null;
            }

            if (InputManager.Instance == null || !InputManager.Instance.TryGetPointerScreenPosition(out var pointerScreenPosition))
            {
                return null;
            }

            var ray = cam.ScreenPointToRay(pointerScreenPosition);
            if (!Physics.Raycast(ray, out var hit, worldRaycastDistance))
            {
                return null;
            }

            var tag = hit.collider.GetComponentInParent<CombatCapsuleTag>();
            if (tag == null || string.IsNullOrEmpty(tag.combatantId))
            {
                return null;
            }

            var hovered = _controller.FindCombatantById(tag.combatantId);
            if (hovered == null || hovered.Health.IsDead)
            {
                return null;
            }

            return hovered;
        }

        public void Tick()
        {
            if (_controller == null || _controller.BattleState == null)
            {
                return;
            }

            if (!TryLockActiveRowToSelection())
            {
                var hovered = TryRaycastHoveredLivingCombatant();
                if (hovered != null && !string.IsNullOrEmpty(hovered.Identity.Id))
                {
                    _activeSkillRowCombatantId = hovered.Identity.Id;
                }
            }

            if (string.IsNullOrEmpty(_activeSkillRowCombatantId))
            {
                HideAllRows();
                return;
            }

            if (!_rowsByCombatantId.TryGetValue(_activeSkillRowCombatantId, out var row) || row == null)
            {
                return;
            }

            var displaySubject = _controller.FindCombatantById(_activeSkillRowCombatantId);
            if (displaySubject == null || displaySubject.Health.IsDead)
            {
                _activeSkillRowCombatantId = null;
                _controller.ClearSkillBarSelection();
                HideAllRows();
                return;
            }

            SetRowVisibilityForActiveId(_activeSkillRowCombatantId);
            if (!row.gameObject.activeInHierarchy)
            {
                return;
            }

            _controller.GetSkillBarSelection(out var selectedSlot, out var owner);
            var canIssue = _controller.IsPlayerCommandingCombatant(displaySubject);
            row.Refresh(
                _controller.BattleState,
                _controller.BattleSimulator,
                displaySubject,
                canIssue,
                selectedSlot,
                owner,
                _controller.CurrentSelectedEnemy);
        }

        private void SetRowVisibilityForActiveId(string activeCombatantId)
        {
            foreach (var idAndRow in _rowsByCombatantId)
            {
                if (idAndRow.Value == null)
                {
                    continue;
                }

                var shouldShow = string.Equals(
                    idAndRow.Key,
                    activeCombatantId,
                    StringComparison.Ordinal);
                if (idAndRow.Value.gameObject.activeSelf == shouldShow)
                {
                    continue;
                }

                idAndRow.Value.gameObject.SetActive(shouldShow);
            }
        }

        private void HideAllRows()
        {
            foreach (var row in _rowsByCombatantId.Values)
            {
                if (row != null)
                {
                    row.gameObject.SetActive(false);
                }
            }
        }
    }
}
