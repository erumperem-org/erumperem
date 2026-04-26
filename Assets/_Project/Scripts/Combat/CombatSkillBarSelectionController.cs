using System;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Centraliza pedidos de seleção de slot: teclas 1–7 (via <see cref="InputManager"/>) e clique nos painéis.
    /// Chama <see cref="CombatPrototypeController.TrySelectSkillBarSlot"/>; emite <see cref="SkillSlotSelected"/> após sucesso.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatSkillBarSelectionController : MonoBehaviour
    {
        [SerializeField] private CombatSkillButtonBarUIManager skillButtonBarUIManager;

        private CombatPrototypeController _combat;
        private bool _subscribedToKeyboard;

        /// <summary>Índice base 0..6 após seleção bem-sucedida no combate.</summary>
        public event Action<int> SkillSlotSelected;

        public void Bind(CombatPrototypeController combatPrototypeController)
        {
            _combat = combatPrototypeController;
            if (skillButtonBarUIManager == null)
            {
                skillButtonBarUIManager = GetComponent<CombatSkillButtonBarUIManager>();
            }

            SubscribeKeyboard();
        }

        private void OnDestroy()
        {
            UnsubscribeKeyboard();
        }

        /// <summary>
        /// Entrada única de seleção (tecla 1–7 e clique no botão). Usa sempre combatente do turno.
        /// </summary>
        public void SelectSkillByIndex(int zeroBasedIndex)
        {
            if (_combat == null)
            {
                return;
            }

            var ownerId = _combat.PendingPlayerCombatantId;
            if (string.IsNullOrEmpty(ownerId))
            {
                return;
            }

            TrySelectForOwner(ownerId, zeroBasedIndex);
        }

        /// <summary>Clique no painel do slot: quando linha é do turno, redireciona para <see cref="SelectSkillByIndex"/>.</summary>
        public void RequestSelectSkillSlot(string ownerCombatantId, int zeroBasedSlot)
        {
            if (_combat == null)
            {
                return;
            }

            if (string.Equals(ownerCombatantId, _combat.PendingPlayerCombatantId, StringComparison.Ordinal))
            {
                // Mouse click e teclado passam pelo mesmo método central.
                SelectSkillByIndex(zeroBasedSlot);
                return;
            }

            TrySelectForOwner(ownerCombatantId, zeroBasedSlot);
        }

        private void TrySelectForOwner(string ownerCombatantId, int zeroBasedSlot)
        {
            if (_combat == null)
            {
                return;
            }

            var selectionSucceeded = _combat.TrySelectSkillBarSlot(ownerCombatantId, zeroBasedSlot);

            if (selectionSucceeded)
            {
                SkillSlotSelected?.Invoke(zeroBasedSlot);
                return;
            }

            if (string.Equals(ownerCombatantId, _combat.PendingPlayerCombatantId, StringComparison.Ordinal))
            {
                _combat.NotifySkillBarSlotRequestFailed(zeroBasedSlot);
            }
        }

        private void SubscribeKeyboard()
        {
            if (_subscribedToKeyboard || InputManager.Instance == null)
            {
                return;
            }

            InputManager.Instance.OnSkillSlotPressed += OnKeyboardSkillSlotPressed;
            _subscribedToKeyboard = true;
        }

        private void UnsubscribeKeyboard()
        {
            if (!_subscribedToKeyboard || InputManager.Instance == null)
            {
                return;
            }

            InputManager.Instance.OnSkillSlotPressed -= OnKeyboardSkillSlotPressed;
            _subscribedToKeyboard = false;
        }

        private void OnKeyboardSkillSlotPressed(int zeroBasedIndex)
        {
            SelectSkillByIndex(zeroBasedIndex);
        }
    }
}
