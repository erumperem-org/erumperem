using System;
using System.Collections.Generic;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Dono único do cursor e da seleção da barra de skills. Teclado e mouse passam pelas mesmas operações:
    /// <list type="bullet">
    /// <item>A/D (ou setas): <see cref="MoveCursor"/> — anda pelo carrossel, dando a volta na lista.</item>
    /// <item>Espaço: <see cref="ConfirmCursor"/> — seleciona a skill do cursor.</item>
    /// <item>Clique num botão ou tecla 1–7: <see cref="ActivateSlot"/> — leva o cursor até a skill e seleciona.</item>
    /// </list>
    /// O cursor é a skill central do carrossel; existe só enquanto o jogador comanda um herói.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatSkillBarSelectionController : MonoBehaviour
    {
        [SerializeField] private CombatSkillButtonBarUIManager skillButtonBarUIManager;

        private readonly Dictionary<string, int> _lastCursorByOwner = new();
        private CombatPrototypeController _combat;
        private bool _subscribedToKeyboard;
        private string _cursorOwnerId;
        private int _cursorSlot;

        /// <summary>Índice base 0..6 após seleção bem-sucedida no combate.</summary>
        public event Action<int> SkillSlotSelected;

        /// <summary>Cursor andou; o argumento é o passo com sinal no carrossel (0 = reposicionado sem deslize).</summary>
        public event Action<int> CursorMoved;

        /// <summary>O jogador tentou usar uma skill indisponível.</summary>
        public event Action<int> SlotRejected;

        public int? CursorSlot => string.IsNullOrEmpty(_cursorOwnerId) ? null : _cursorSlot;

        public string CursorOwnerId => _cursorOwnerId;

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
        /// Acompanha o turno: o cursor nasce na última skill usada pelo herói (ou na primeira)
        /// e fica preso na skill selecionada enquanto houver seleção.
        /// </summary>
        public void SyncWithTurn()
        {
            if (!TryGetCommandedOwner(out var ownerId, out var slotCount))
            {
                ReleaseCursor();
                return;
            }

            if (!string.Equals(ownerId, _cursorOwnerId, StringComparison.Ordinal))
            {
                ReleaseCursor();
                _cursorOwnerId = ownerId;
                _cursorSlot = _lastCursorByOwner.TryGetValue(ownerId, out var remembered) ? remembered : 0;
                _cursorSlot = Mathf.Clamp(_cursorSlot, 0, slotCount - 1);
                CursorMoved?.Invoke(0);
            }
            else if (_cursorSlot >= slotCount)
            {
                SetCursor(slotCount - 1, slotCount);
            }

            if (TryGetSelectedSlot(out var selectedSlot) && selectedSlot != _cursorSlot)
            {
                SetCursor(selectedSlot, slotCount);
            }
        }

        public void MoveCursor(int step)
        {
            SyncWithTurn();
            if (step == 0 || !TryGetCommandedOwner(out _, out var slotCount))
            {
                return;
            }

            SetCursor(Wrap(_cursorSlot + step, slotCount), slotCount, step);
        }

        public void ConfirmCursor()
        {
            SyncWithTurn();
            if (string.IsNullOrEmpty(_cursorOwnerId))
            {
                return;
            }

            TrySelect(_cursorSlot);
        }

        public void ActivateSlot(int zeroBasedSlot) => ActivateSlot(zeroBasedSlot, null);

        /// <summary>Clique num botão do carrossel: o deslize segue a posição clicada (importa quando a skill aparece dos dois lados).</summary>
        public void ActivateSlotAtCarouselOffset(int zeroBasedSlot, int offsetFromCenter) =>
            ActivateSlot(zeroBasedSlot, offsetFromCenter);

        private void ActivateSlot(int zeroBasedSlot, int? carouselStep)
        {
            SyncWithTurn();
            if (!TryGetCommandedOwner(out _, out var slotCount) ||
                zeroBasedSlot < 0 ||
                zeroBasedSlot >= slotCount)
            {
                return;
            }

            SetCursor(zeroBasedSlot, slotCount, carouselStep);
            TrySelect(zeroBasedSlot);
        }

        private void TrySelect(int zeroBasedSlot)
        {
            if (TryGetSelectedSlot(out var selectedSlot) && selectedSlot == zeroBasedSlot)
            {
                return;
            }

            if (_combat.TrySelectSkillBarSlot(_cursorOwnerId, zeroBasedSlot))
            {
                SkillSlotSelected?.Invoke(zeroBasedSlot);
                return;
            }

            // Escolher outra skill que não pode ser usada desfaz a seleção anterior: o cursor e a seleção não divergem.
            _combat.ClearSkillBarSelection();
            _combat.NotifySkillBarSlotRequestFailed(zeroBasedSlot);
            SlotRejected?.Invoke(zeroBasedSlot);
        }

        private void SetCursor(int zeroBasedSlot, int slotCount, int? carouselStep = null)
        {
            if (zeroBasedSlot == _cursorSlot)
            {
                return;
            }

            var step = carouselStep ?? ShortestRingStep(_cursorSlot, zeroBasedSlot, slotCount);
            _cursorSlot = zeroBasedSlot;
            _lastCursorByOwner[_cursorOwnerId] = zeroBasedSlot;
            CursorMoved?.Invoke(step);
        }

        private void ReleaseCursor()
        {
            if (string.IsNullOrEmpty(_cursorOwnerId))
            {
                return;
            }

            _lastCursorByOwner[_cursorOwnerId] = _cursorSlot;
            _cursorOwnerId = null;
            _cursorSlot = 0;
        }

        private bool TryGetCommandedOwner(out string ownerId, out int slotCount)
        {
            ownerId = null;
            slotCount = 0;

            if (_combat == null || _combat.BattleState == null || !_combat.IsBattleOngoing)
            {
                return false;
            }

            var pendingId = _combat.PendingPlayerCombatantId;
            if (string.IsNullOrEmpty(pendingId))
            {
                return false;
            }

            var actor = _combat.FindCombatantById(pendingId);
            if (actor == null || actor.Health.IsDead || !_combat.IsPlayerCommandingCombatant(actor))
            {
                return false;
            }

            slotCount = CombatSkillSlotUiEligibility.CountSlots(_combat.BattleState, actor);
            if (slotCount <= 0)
            {
                return false;
            }

            ownerId = pendingId;
            return true;
        }

        private bool TryGetSelectedSlot(out int zeroBasedSlot)
        {
            zeroBasedSlot = default;
            if (_combat == null || string.IsNullOrEmpty(_cursorOwnerId))
            {
                return false;
            }

            _combat.GetSkillBarSelection(out var selectedSlot, out var selectedOwnerId);
            if (!selectedSlot.HasValue ||
                !string.Equals(selectedOwnerId, _cursorOwnerId, StringComparison.Ordinal))
            {
                return false;
            }

            zeroBasedSlot = selectedSlot.Value;
            return true;
        }

        private static int Wrap(int index, int count) => ((index % count) + count) % count;

        /// <summary>Passo com sinal pelo caminho mais curto no anel (ex.: 6 → 0 com 7 skills = +1).</summary>
        private static int ShortestRingStep(int from, int to, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            var forward = Wrap(to - from, count);
            return forward > count / 2 ? forward - count : forward;
        }

        private void SubscribeKeyboard()
        {
            if (_subscribedToKeyboard || InputManager.Instance == null)
            {
                return;
            }

            InputManager.Instance.OnSkillSlotPressed += ActivateSlot;
            _subscribedToKeyboard = true;
        }

        private void UnsubscribeKeyboard()
        {
            if (!_subscribedToKeyboard || InputManager.Instance == null)
            {
                return;
            }

            InputManager.Instance.OnSkillSlotPressed -= ActivateSlot;
            _subscribedToKeyboard = false;
        }
    }
}
