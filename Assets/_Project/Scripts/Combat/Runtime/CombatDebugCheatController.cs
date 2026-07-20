using System;
using Erumperem.Combat.HealthBars;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Debug combat cheats toggled from <see cref="InputManager"/> during battle.
    /// </summary>
    public sealed class CombatDebugCheatController
    {
        private readonly CombatSessionRuntime _session;
        private readonly CombatUnitVisualSynchronizer _unitVisualSynchronizer;
        private readonly float _enemyDeathClipMarginSeconds;

        public CombatDebugCheatController(
            CombatSessionRuntime session,
            CombatUnitVisualSynchronizer unitVisualSynchronizer,
            float enemyDeathClipMarginSeconds)
        {
            _session = session;
            _unitVisualSynchronizer = unitVisualSynchronizer;
            _enemyDeathClipMarginSeconds = enemyDeathClipMarginSeconds;
        }

        public void ToggleInfiniteAllyHealthCheat()
        {
            if (_session.State == null)
            {
                Debug.LogWarning("Cheat F9 ignorado: combate ainda não está pronto.");
                return;
            }

            if (_session.BattleEnded)
            {
                Debug.Log("Cheat F9 ignorado: combate já terminou.");
                return;
            }

            if (_session.IsInfiniteAllyHealthCheatActive)
            {
                DisableInfiniteAllyHealthCheat(restoreSavedHealth: true);
                Debug.Log("Cheat F9: vida infinita dos aliados DESLIGADA — HP restaurado ao valor anterior.");
                return;
            }

            SnapshotAllyHealthForInfiniteHealthCheat();
            _session.IsInfiniteAllyHealthCheatActive = true;
            _session.State.AlliesHaveInfiniteHealth = true;
            Debug.Log("Cheat F9: vida infinita dos aliados LIGADA.");
        }

        public void ToggleDoubleAllyDamageCheat()
        {
            if (_session.State == null)
            {
                Debug.LogWarning("Cheat F10 ignorado: combate ainda não está pronto.");
                return;
            }

            if (_session.BattleEnded)
            {
                Debug.Log("Cheat F10 ignorado: combate já terminou.");
                return;
            }

            if (_session.IsDoubleAllyDamageCheatActive)
            {
                _session.IsDoubleAllyDamageCheatActive = false;
                _session.State.AllyOutgoingDamageMultiplier = 1.0;
                Debug.Log("Cheat F10: dano ×2 dos aliados DESLIGADO.");
                return;
            }

            _session.IsDoubleAllyDamageCheatActive = true;
            _session.State.AllyOutgoingDamageMultiplier = 2.0;
            Debug.Log("Cheat F10: dano ×2 dos aliados LIGADO.");
        }

        public void DebugKillAllEnemiesInstantly(Action clearSkillBarSelection)
        {
            if (_session.State == null)
            {
                Debug.LogWarning("Cheat F6 ignorado: combate ainda não está pronto.");
                return;
            }

            if (_session.BattleEnded)
            {
                Debug.Log("Cheat F6 ignorado: combate já terminou.");
                return;
            }

            var killedAtLeastOne = false;
            foreach (var enemy in _session.State.Enemies)
            {
                if (enemy.Health.IsDead)
                {
                    continue;
                }

                enemy.Health.CurrentHp = 0;
                enemy.Health.IsDead = true;
                killedAtLeastOne = true;
                _session.Simulator.EmitCombatantDied(_session.State, enemy.Identity.Id);

                if (_unitVisualSynchronizer.TryGetAnimationController(
                        enemy.Identity.Id,
                        out var enemyAnimationController))
                {
                    enemyAnimationController.EnsureDeathVisualSequenceStarted(_enemyDeathClipMarginSeconds);
                }
            }

            if (!killedAtLeastOne)
            {
                Debug.Log("Cheat F6 ignorado: todos os inimigos já estavam mortos.");
                return;
            }

            Debug.Log("Cheat F6 acionado: inimigos mortos instantaneamente para testar a tela de vitória.");
            _session.NeedsPlayerInput = false;
            _session.PendingPlayerActor = null;
            clearSkillBarSelection?.Invoke();
        }

        public void DebugKillAllAlliesInstantly(Action clearSkillBarSelection)
        {
            if (_session.State == null)
            {
                Debug.LogWarning("Cheat F7 ignorado: combate ainda não está pronto.");
                return;
            }

            if (_session.BattleEnded)
            {
                Debug.Log("Cheat F7 ignorado: combate já terminou.");
                return;
            }

            var killedAtLeastOne = false;
            foreach (var ally in _session.State.Allies)
            {
                if (ally.Health.IsDead)
                {
                    continue;
                }

                ally.Health.CurrentHp = 0;
                ally.Health.IsDead = true;
                killedAtLeastOne = true;
                _session.Simulator.EmitCombatantDied(_session.State, ally.Identity.Id);

                if (_unitVisualSynchronizer.TryGetAnimationController(
                        ally.Identity.Id,
                        out var allyAnimationController))
                {
                    allyAnimationController.EnsureDeathVisualSequenceStarted(_enemyDeathClipMarginSeconds);
                }
            }

            if (!killedAtLeastOne)
            {
                Debug.Log("Cheat F7 ignorado: todos os aliados já estavam mortos.");
                return;
            }

            Debug.Log("Cheat F7 acionado: aliados mortos instantaneamente para testar a tela de derrota.");
            _session.NeedsPlayerInput = false;
            _session.PendingPlayerActor = null;
            clearSkillBarSelection?.Invoke();
        }

        public void ClearAllCombatCheats()
        {
            DisableInfiniteAllyHealthCheat(restoreSavedHealth: true);
            DisableDoubleAllyDamageCheat();
        }

        private void SnapshotAllyHealthForInfiniteHealthCheat()
        {
            _session.AllyHealthBeforeInfiniteHealthCheat.Clear();
            foreach (var ally in _session.State.Allies)
            {
                _session.AllyHealthBeforeInfiniteHealthCheat[ally.Identity.Id] =
                    new CombatSessionRuntime.AllyHealthCheatSnapshot(ally.Health.CurrentHp, ally.Health.IsDead);
            }
        }

        private void DisableInfiniteAllyHealthCheat(bool restoreSavedHealth)
        {
            _session.IsInfiniteAllyHealthCheatActive = false;
            if (_session.State != null)
            {
                _session.State.AlliesHaveInfiniteHealth = false;
            }

            if (!restoreSavedHealth || _session.State == null)
            {
                _session.AllyHealthBeforeInfiniteHealthCheat.Clear();
                return;
            }

            foreach (var ally in _session.State.Allies)
            {
                if (!_session.AllyHealthBeforeInfiniteHealthCheat.TryGetValue(ally.Identity.Id, out var snapshot))
                {
                    continue;
                }

                ally.Health.CurrentHp = Math.Max(0, Math.Min(snapshot.CurrentHp, ally.Health.MaxHp));
                ally.Health.IsDead = snapshot.IsDead;
                ally.Health.IsDeathblowPending = false;
            }

            _session.AllyHealthBeforeInfiniteHealthCheat.Clear();
            InvalidateAllyHealthBarDisplays();
        }

        private void DisableDoubleAllyDamageCheat()
        {
            _session.IsDoubleAllyDamageCheatActive = false;
            if (_session.State != null)
            {
                _session.State.AllyOutgoingDamageMultiplier = 1.0;
            }
        }

        private static void InvalidateAllyHealthBarDisplays()
        {
            foreach (var healthBarHudView in UnityEngine.Object.FindObjectsByType<HealthBarHudView>(FindObjectsSortMode.None))
            {
                healthBarHudView.InvalidateHealthDisplayCache();
            }
        }
    }
}
