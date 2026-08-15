using System;
using System.Collections.Generic;
using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Engine;
using Game.Core.Models;
using UnityEngine;

namespace Erumperem.Combat.Runtime
{
    /// <summary>
    /// Mutable battle session state shared by <see cref="CombatPrototypeController"/> collaborators.
    /// </summary>
    public sealed class CombatSessionRuntime
    {
        public BattleState State { get; set; }
        public BattleSimulator Simulator { get; set; }
        public CombatEventCollector EventCollector { get; set; }
        public SeededRandomSource Random { get; set; }

        public readonly List<Combatant> RoundOrder = new();
        public int ActorIndex;
        public bool PreparedThisStep;
        public bool BattleEnded;
        public bool NeedsPlayerInput;
        public Combatant PendingPlayerActor;

        public readonly Dictionary<string, Transform> UnitVisualRootsByCombatantId =
            new(StringComparer.Ordinal);

        public readonly Dictionary<string, EnemyVisualDefinition> EnemyVisualByCombatantId =
            new(StringComparer.Ordinal);

        public readonly HashSet<string> DamageFeedbackBusy = new(StringComparer.Ordinal);

        public bool PresentationBusy;
        public string OngoingPresentationActorCombatantId = string.Empty;
        public string OngoingPresentationTargetCombatantId = string.Empty;

        public Combatant SelectedEnemyTarget;
        public int? SkillBarSelectedSlot;
        public string SkillBarSelectedOwnerId;

        public bool IsInfiniteAllyHealthCheatActive;
        public bool IsDoubleAllyDamageCheatActive;
        public readonly Dictionary<string, AllyHealthCheatSnapshot> AllyHealthBeforeInfiniteHealthCheat =
            new(StringComparer.Ordinal);

        public bool IsBattleOngoing => !BattleEnded && State != null;

        public bool IsActionPresentationOngoing => PresentationBusy && !BattleEnded;

        public bool HasSkillBarSelectionPendingUse() =>
            SkillBarSelectedSlot.HasValue && !string.IsNullOrEmpty(SkillBarSelectedOwnerId);

        public Combatant FindCombatantById(string combatantId)
        {
            if (State == null || string.IsNullOrEmpty(combatantId))
            {
                return null;
            }

            foreach (var ally in State.Allies)
            {
                if (ally.Identity.Id == combatantId)
                {
                    return ally;
                }
            }

            foreach (var enemy in State.Enemies)
            {
                if (enemy.Identity.Id == combatantId)
                {
                    return enemy;
                }
            }

            return null;
        }

        public readonly struct AllyHealthCheatSnapshot
        {
            public AllyHealthCheatSnapshot(int currentHp, bool isDead)
            {
                CurrentHp = currentHp;
                IsDead = isDead;
            }

            public int CurrentHp { get; }
            public bool IsDead { get; }
        }
    }
}
