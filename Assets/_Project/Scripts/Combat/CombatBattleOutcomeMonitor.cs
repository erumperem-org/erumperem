using System;
using Game.Core.Analytics;
using Game.Core.Models;

namespace Erumperem.Combat
{
    /// <summary>
    /// Subscreve <see cref="CombatEventCollector.CombatantDied"/> e termina o combate quando
    /// um dos lados fica sem combatentes activos (vitória ou derrota).
    /// </summary>
    public sealed class CombatBattleOutcomeMonitor
    {
        private BattleState _battleState;
        private CombatEventCollector _eventCollector;
        private Action _onBattleShouldEnd;

        public void Begin(
            BattleState battleState,
            CombatEventCollector eventCollector,
            Action onBattleShouldEnd)
        {
            End();

            _battleState = battleState;
            _eventCollector = eventCollector;
            _onBattleShouldEnd = onBattleShouldEnd;
            _eventCollector.CombatantDied += HandleCombatantDied;
        }

        public void End()
        {
            if (_eventCollector != null)
            {
                _eventCollector.CombatantDied -= HandleCombatantDied;
            }

            _battleState = null;
            _eventCollector = null;
            _onBattleShouldEnd = null;
        }

        private void HandleCombatantDied(CombatEvent combatEvent)
        {
            if (_battleState == null || _onBattleShouldEnd == null)
            {
                return;
            }

            _battleState.SyncDeathFlagsFromHealth();

            if (!_battleState.IsFinished)
            {
                return;
            }

            _onBattleShouldEnd.Invoke();
        }
    }
}
