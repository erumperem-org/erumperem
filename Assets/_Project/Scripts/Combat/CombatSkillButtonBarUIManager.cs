using System;
using Game.Core.Models;
using UnityEngine;
using UnityEngine.Serialization;

namespace Erumperem.Combat
{
    /// <summary>
    /// Spawns combat skill buttons inside the Combat HUD <c>SkillsPanel</c> for the active combatant.
    /// </summary>
    public sealed class CombatSkillButtonBarUIManager : MonoBehaviour
    {
        [FormerlySerializedAs("rowsParent")]
        [SerializeField] private Transform skillsPanelParent;
        [FormerlySerializedAs("skillButtonPanelPrefab")]
        [SerializeField] private GameObject skillButtonCombatPrefab;
        [SerializeField] private SkillVisualCatalog skillVisualCatalog;
        [SerializeField] private float worldRaycastDistance = 200f;
        [SerializeField] private CombatSkillBarSelectionController skillBarSelectionController;

        private CombatPrototypeController _controller;
        private CharacterSkillButtonsRowView _skillsRowView;
        private string _activeSkillRowCombatantId;

        private void Awake()
        {
            TryResolveSkillsPanelParentIfMissing();
        }

        public void Initialize(CombatPrototypeController controller)
        {
            _controller = controller;
            TryResolveSkillsPanelParentIfMissing();
            if (skillsPanelParent == null)
            {
                skillsPanelParent = transform;
            }

            if (skillBarSelectionController == null)
            {
                skillBarSelectionController = GetComponent<CombatSkillBarSelectionController>();
            }

            skillBarSelectionController?.Bind(controller);

            if (_controller == null || skillButtonCombatPrefab == null)
            {
                return;
            }

            _skillsRowView = skillsPanelParent.GetComponent<CharacterSkillButtonsRowView>();
            if (_skillsRowView == null)
            {
                _skillsRowView = skillsPanelParent.gameObject.AddComponent<CharacterSkillButtonsRowView>();
            }

            _skillsRowView.Build(
                this,
                skillButtonCombatPrefab,
                skillVisualCatalog,
                skillBarSelectionController);
            _skillsRowView.HideAllSlots();
            _activeSkillRowCombatantId = null;
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
            _skillsRowView?.HideAllSlots();
        }

        public void OnSkillBarSelectionCleared()
        {
            SyncVisibleRowWithBattle();
        }

        public void SyncVisibleRowWithBattle()
        {
            if (_skillsRowView == null || _controller == null || string.IsNullOrEmpty(_activeSkillRowCombatantId))
            {
                return;
            }

            TryLockActiveRowToSelection();
            var subject = _controller.FindCombatantById(_activeSkillRowCombatantId);
            if (subject == null)
            {
                return;
            }

            _controller.GetSkillBarSelection(out var slot, out var owner);
            var canIssue = _controller.IsPlayerCommandingCombatant(subject);
            _skillsRowView.SetActiveCombatantId(_activeSkillRowCombatantId);
            _skillsRowView.Refresh(
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

        public bool TryGetHoveredLivingCombatant(out Combatant hoveredCombatant)
        {
            hoveredCombatant = TryRaycastHoveredLivingCombatant();
            return hoveredCombatant != null;
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
            if (_controller == null || _controller.BattleState == null || _skillsRowView == null)
            {
                return;
            }

            if (!TryLockActiveRowToSelection())
            {
                var pendingPlayerCombatantId = _controller.PendingPlayerCombatantId;
                if (!string.IsNullOrEmpty(pendingPlayerCombatantId))
                {
                    _activeSkillRowCombatantId = pendingPlayerCombatantId;
                }

                var hovered = TryRaycastHoveredLivingCombatant();
                if (string.IsNullOrEmpty(_activeSkillRowCombatantId) &&
                    hovered != null &&
                    !string.IsNullOrEmpty(hovered.Identity.Id))
                {
                    _activeSkillRowCombatantId = hovered.Identity.Id;
                }
            }

            if (string.IsNullOrEmpty(_activeSkillRowCombatantId))
            {
                _skillsRowView.HideAllSlots();
                return;
            }

            var displaySubject = _controller.FindCombatantById(_activeSkillRowCombatantId);
            if (displaySubject == null || displaySubject.Health.IsDead)
            {
                _activeSkillRowCombatantId = null;
                _controller.ClearSkillBarSelection();
                _skillsRowView.HideAllSlots();
                return;
            }

            _controller.GetSkillBarSelection(out var selectedSlot, out var owner);
            var canIssue = _controller.IsPlayerCommandingCombatant(displaySubject);
            _skillsRowView.SetActiveCombatantId(_activeSkillRowCombatantId);
            _skillsRowView.Refresh(
                _controller.BattleState,
                _controller.BattleSimulator,
                displaySubject,
                canIssue,
                selectedSlot,
                owner,
                _controller.CurrentSelectedEnemy);
        }

        private void TryResolveSkillsPanelParentIfMissing()
        {
            if (skillsPanelParent != null)
            {
                return;
            }

            var skillsPanelAnchor = FindFirstObjectByType<CombatSkillsPanelAnchor>();
            if (skillsPanelAnchor != null)
            {
                skillsPanelParent = skillsPanelAnchor.transform;
            }
        }
    }
}
