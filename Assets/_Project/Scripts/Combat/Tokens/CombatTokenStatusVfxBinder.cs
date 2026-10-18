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
            if (_presenters.Count == 0 || _combatSession == null || !_combatSession.IsBattleOngoing)
            {
                return;
            }

            foreach (var presenter in _presenters)
            {
                presenter?.RefreshFromBattleState();
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

            foreach (var combatant in battleState.GetAllCombatants())
            {
                var unitVisualRoot = controller.TryGetUnitVisualRoot(combatant.Identity.Id);
                if (unitVisualRoot == null)
                {
                    continue;
                }

                var presenter = new CombatantTokenStatusVfxPresenter(
                    controller,
                    combatant.Identity.Id,
                    tokenVisualCatalog,
                    unitVisualRoot);
                presenter.RefreshFromBattleState();
                _presenters.Add(presenter);
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
        }
    }
}
