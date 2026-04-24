using System;
using System.Collections.Generic;
using Game.Core.Models;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Erumperem.Combat
{
    /// <summary>
    /// Uma linha de skills por combatente, uma visível; hover no 3D controla o visível; cores vêm de
    /// <see cref="SkillUiColorPalette"/>. A seleção fica em <see cref="CombatPrototypeController"/>.
    /// </summary>
    public sealed class CombatSkillButtonBarUIManager : MonoBehaviour
    {
        [SerializeField] private Transform rowsParent;
        [SerializeField] private GameObject characterSkillButtonsRowPrefab;
        [SerializeField] private GameObject skillButtonPanelPrefab;
        [SerializeField] private float worldRaycastDistance = 200f;

        private CombatPrototypeController _controller;
        private readonly Dictionary<string, CharacterSkillButtonsRowView> _rowsByCombatantId =
            new(StringComparer.Ordinal);
        /// <summary>Combatente cuja barra fica visível: só muda ao passar o rato sobre <b>outro</b> personagem, não ao sair para o vazio.</summary>
        private string _activeSkillRowCombatantId;
        private Camera _combatCamera;

        public void Initialize(CombatPrototypeController controller)
        {
            _controller = controller;
            _combatCamera = Camera.main;
            if (rowsParent == null)
            {
                rowsParent = transform;
            }

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
                var row = rowObject.GetComponent<CharacterSkillButtonsRowView>() ?? rowObject.AddComponent<CharacterSkillButtonsRowView>();
                row.Build(this, id, skillButtonPanelPrefab);
                _rowsByCombatantId[id] = row;
            }
        }

        public void NotifySkillBarSlotSelected(string ownerCombatantId, int zeroBasedSlot)
        {
            if (_controller == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(ownerCombatantId) || (zeroBasedSlot < 0 || zeroBasedSlot > 6))
            {
                return;
            }

            _controller.SetSkillBarSelectionFromUi(ownerCombatantId, zeroBasedSlot);
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

        public void Tick()
        {
            if (_controller == null || _controller.BattleState == null)
            {
                return;
            }

            if (_combatCamera == null)
            {
                _combatCamera = Camera.main;
            }

            if (_combatCamera == null)
            {
                return;
            }

            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var ray = _combatCamera.ScreenPointToRay(mouse.position.ReadValue());
            Combatant hovered = null;
            if (Physics.Raycast(ray, out var hit, worldRaycastDistance))
            {
                var tag = hit.collider.GetComponentInParent<CombatCapsuleTag>();
                if (tag != null && !string.IsNullOrEmpty(tag.combatantId))
                {
                    hovered = _controller.FindCombatantById(tag.combatantId);
                    if (hovered != null && hovered.Health.IsDead)
                    {
                        hovered = null;
                    }
                }
            }

            var hoveredId = hovered?.Identity?.Id;
            if (!string.IsNullOrEmpty(hoveredId))
            {
                if (!string.IsNullOrEmpty(_activeSkillRowCombatantId) &&
                    !string.Equals(_activeSkillRowCombatantId, hoveredId, StringComparison.Ordinal))
                {
                    _controller.ClearSkillBarSelection();
                }

                _activeSkillRowCombatantId = hoveredId;
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
