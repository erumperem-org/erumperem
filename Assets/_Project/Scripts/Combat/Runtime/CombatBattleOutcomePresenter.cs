using Game.Core.Domain;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Presents victory/defeat UI and exploration bridge notifications when a battle ends.
    /// </summary>
    public sealed class CombatBattleOutcomePresenter
    {
        private readonly CombatSessionRuntime _session;
        private readonly CombatSessionHub _sessionHub;
        private readonly GameObject _victoryPanel;
        private readonly GameObject _defeatPanel;
        private readonly bool _logEventsToConsole;

        public CombatBattleOutcomePresenter(
            CombatSessionRuntime session,
            CombatSessionHub sessionHub,
            GameObject victoryPanel,
            GameObject defeatPanel,
            bool logEventsToConsole)
        {
            _session = session;
            _sessionHub = sessionHub;
            _victoryPanel = victoryPanel;
            _defeatPanel = defeatPanel;
            _logEventsToConsole = logEventsToConsole;
        }

        public void EndBattle(System.Action clearAllCombatCheats, System.Action clearSkillBarSelection)
        {
            if (_session.BattleEnded)
            {
                return;
            }

            _session.BattleEnded = true;
            _session.NeedsPlayerInput = false;
            clearSkillBarSelection?.Invoke();
            clearAllCombatCheats?.Invoke();
            _session.Simulator.EmitBattleEnded(_session.State);
            LogLastEvents();

            if (_session.State.Winner == Side.Allies)
            {
                _victoryPanel.SetActive(true);
                _victoryPanel.GetComponent<CorruptionRewardGenerator>().GenerateRewards();
            }
            else if (_session.State.Winner == Side.Enemies)
            {
                _defeatPanel.SetActive(true);
                UnityEngine.Object.FindAnyObjectByType<PlayerInventorySaveSystem>().ClearSave();
            }
            else
            {
                Debug.Log("Empate?");
            }

            CombatExplorationBridge.Instance?.NotifyCombatEnded(
                _session.State,
                alliesWon: _session.State.Winner == Side.Allies);

            _sessionHub?.RaiseCombatSessionClosed();
        }

        private void LogLastEvents()
        {
            if (!_logEventsToConsole || _session.EventCollector.Events.Count == 0)
            {
                return;
            }

            var lastEvent = _session.EventCollector.Events[^1];
            Debug.Log(
                $"[Combat] {lastEvent.EventType} turn={lastEvent.Turn} actor={lastEvent.ActorId} " +
                $"target={lastEvent.TargetId} skill={lastEvent.SkillId} dmg={lastEvent.DamageAmount}");
        }
    }
}
