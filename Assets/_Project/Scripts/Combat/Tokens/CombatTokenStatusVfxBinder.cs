using System.Collections.Generic;
using Erumperem.Combat;
using UnityEngine;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// Spawns one <see cref="CombatantTokenStatusVfxPresenter"/> per combatant with a unit visual root
    /// and keeps body token VFX in sync with <see cref="Game.Core.Models.Combatant.Tokens"/>.
    /// </summary>
    [DefaultExecutionOrder(26)]
    public sealed class CombatTokenStatusVfxBinder : MonoBehaviour
    {
        [SerializeField] private CombatSessionHub sessionHub;
        [SerializeField] private TokenVisualCatalog tokenVisualCatalog;

        private CombatPrototypeController _combatSession;
        private readonly List<CombatantTokenStatusVfxPresenter> _presenters = new();
        private readonly HashSet<string> _presenterCombatantIds = new();

        private void OnEnable()
        {
            if (sessionHub == null)
            {
                return;
            }

            sessionHub.OnCombatSessionReadyForUi += HandleCombatSessionReadyForUi;
            sessionHub.OnCombatSessionClosed += HandleCombatSessionClosed;
        }

        private void OnDisable()
        {
            if (sessionHub == null)
            {
                return;
            }

            sessionHub.OnCombatSessionReadyForUi -= HandleCombatSessionReadyForUi;
            sessionHub.OnCombatSessionClosed -= HandleCombatSessionClosed;
            TearDownPresenters();
        }

        private void LateUpdate()
        {
            if (_combatSession == null || !_combatSession.IsBattleOngoing)
            {
                return;
            }

            EnsurePresentersForSpawnedCombatants();

            foreach (var presenter in _presenters)
            {
                presenter?.RefreshFromBattleState();
            }
        }

        private void EnsurePresentersForSpawnedCombatants()
        {
            var battleState = _combatSession.BattleState;
            if (battleState == null || tokenVisualCatalog == null)
            {
                return;
            }

            foreach (var combatant in battleState.GetAllCombatants())
            {
                var combatantId = combatant.Identity.Id;
                if (string.IsNullOrEmpty(combatantId) || !_presenterCombatantIds.Add(combatantId))
                {
                    continue;
                }

                var unitVisualRoot = _combatSession.TryGetUnitVisualRoot(combatantId);
                if (unitVisualRoot == null)
                {
                    _presenterCombatantIds.Remove(combatantId);
                    continue;
                }

                _presenters.Add(new CombatantTokenStatusVfxPresenter(
                    _combatSession,
                    combatantId,
                    tokenVisualCatalog,
                    unitVisualRoot));
            }
        }

        private void HandleCombatSessionReadyForUi(CombatPrototypeController controller)
        {
            TearDownPresenters();
            if (controller == null || tokenVisualCatalog == null)
            {
                return;
            }

            _combatSession = controller;
            var battleState = controller.BattleState;
            if (battleState == null)
            {
                return;
            }

            EnsurePresentersForSpawnedCombatants();
            foreach (var presenter in _presenters)
            {
                presenter.RefreshFromBattleState();
            }
        }

        private void HandleCombatSessionClosed()
        {
            _combatSession = null;
            TearDownPresenters();
        }

        private void TearDownPresenters()
        {
            foreach (var presenter in _presenters)
            {
                presenter?.TearDown();
            }

            _presenters.Clear();
            _presenterCombatantIds.Clear();
        }
    }
}
