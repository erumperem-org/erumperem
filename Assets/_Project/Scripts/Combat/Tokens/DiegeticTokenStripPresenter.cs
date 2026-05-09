using System.Collections.Generic;
using Game.Core.Domain;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// Horizontal strip: <see cref="Combatant.Tokens"/> (<see cref="TokenType"/>) and active DOTs
    /// (<see cref="Combatant.Dots"/> / <see cref="DotType"/>) using <see cref="TokenVisualCatalog"/>.
    /// </summary>
    public sealed class DiegeticTokenStripPresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform stripContentRoot;
        [SerializeField] private DiegeticTokenIconSlot tokenIconSlotPrefab;
        [SerializeField] private TokenVisualCatalog catalog;

        private readonly Dictionary<TokenType, DiegeticTokenIconSlot> _slotsByTokenType = new();
        private readonly Dictionary<DotType, DiegeticTokenIconSlot> _slotsByDotType = new();
        private CombatPrototypeController _combatSession;
        private string _combatantId = "";

        public void Configure(
            CombatPrototypeController combatSession,
            string combatantId,
            TokenVisualCatalog tokenVisualCatalog)
        {
            _combatSession = combatSession;
            _combatantId = combatantId ?? "";
            catalog = tokenVisualCatalog;
            EnsureSlotsForCatalog();
            EnsureDotSlotsForCatalog();
        }

        private void OnValidate()
        {
            if (stripContentRoot == null)
            {
                stripContentRoot = GetComponent<RectTransform>();
            }
        }

        private void Awake()
        {
            if (stripContentRoot == null)
            {
                stripContentRoot = GetComponent<RectTransform>();
            }
        }

        /// <summary>
        /// Call after battle state mutates (e.g. from binder <see cref="LateUpdate"/> or session events).
        /// </summary>
        public void RefreshFromBattleState()
        {
            if (_combatSession == null || string.IsNullOrEmpty(_combatantId) || catalog == null)
            {
                SetStripVisible(false);
                return;
            }

            if (!_combatSession.IsBattleOngoing)
            {
                SetStripVisible(false);
                return;
            }

            var combatant = _combatSession.FindCombatantById(_combatantId);
            if (combatant == null || combatant.Health.IsDead)
            {
                SetStripVisible(false);
                return;
            }

            SetStripVisible(true);
            RefreshSlots(combatant);
        }

        private void SetStripVisible(bool visible)
        {
            if (stripContentRoot != null)
            {
                stripContentRoot.gameObject.SetActive(visible);
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        private void EnsureSlotsForCatalog()
        {
            if (stripContentRoot == null || tokenIconSlotPrefab == null || catalog == null)
            {
                return;
            }

            foreach (var definition in catalog.Entries)
            {
                if (definition == null)
                {
                    continue;
                }

                var tokenType = definition.TokenType;
                if (_slotsByTokenType.ContainsKey(tokenType))
                {
                    continue;
                }

                var slotInstance = Instantiate(tokenIconSlotPrefab, stripContentRoot);
                slotInstance.name = $"TokenSlot_{tokenType}";
                slotInstance.SetCellActive(false);
                _slotsByTokenType[tokenType] = slotInstance;
            }
        }

        private void EnsureDotSlotsForCatalog()
        {
            if (stripContentRoot == null || tokenIconSlotPrefab == null || catalog == null)
            {
                return;
            }

            foreach (var definition in catalog.DotEntries)
            {
                if (definition == null)
                {
                    continue;
                }

                var dotType = definition.DotType;
                if (_slotsByDotType.ContainsKey(dotType))
                {
                    continue;
                }

                var slotInstance = Instantiate(tokenIconSlotPrefab, stripContentRoot);
                slotInstance.name = $"DotSlot_{dotType}";
                slotInstance.SetCellActive(false);
                _slotsByDotType[dotType] = slotInstance;
            }
        }

        private void RefreshSlots(Combatant combatant)
        {
            var anyVisible = false;
            foreach (var definition in catalog.Entries)
            {
                if (definition == null || !_slotsByTokenType.TryGetValue(definition.TokenType, out var slot))
                {
                    continue;
                }

                var stacks = combatant.Tokens.GetStacks(definition.TokenType);
                if (stacks <= 0)
                {
                    slot.SetCellActive(false);
                    continue;
                }

                anyVisible = true;
                slot.SetCellActive(true);
                slot.ApplyVisual(
                    definition.icon,
                    definition.iconColor,
                    definition.backgroundTint,
                    stacks,
                    showBackgroundTint: true);
            }

            foreach (var definition in catalog.DotEntries)
            {
                if (definition == null || !_slotsByDotType.TryGetValue(definition.DotType, out var slot))
                {
                    continue;
                }

                var dotTurnsSum = SumRemainingDotTurns(combatant.Dots, definition.DotType);
                if (dotTurnsSum <= 0)
                {
                    slot.SetCellActive(false);
                    continue;
                }

                anyVisible = true;
                slot.SetCellActive(true);
                slot.ApplyVisual(
                    definition.icon,
                    definition.iconColor,
                    definition.backgroundTint,
                    dotTurnsSum,
                    showBackgroundTint: true);
            }

            if (!anyVisible)
            {
                SetStripVisible(false);
            }
        }

        /// <summary>
        /// Keeps UI compiling against Game.Core builds that may not yet include <c>DotComponent.SumRemainingTurns</c>.
        /// </summary>
        private static int SumRemainingDotTurns(DotComponent dots, DotType dotType)
        {
            var sum = 0;
            foreach (var dot in dots.ActiveDots)
            {
                if (dot.Type == dotType)
                {
                    sum += dot.RemainingTurns;
                }
            }

            return sum;
        }
    }
}
