using System.Linq;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Passives;

/// <summary>
/// Observer hub for combat passives. Built-in <see cref="PassiveRuleApplier"/> runs first; then <see cref="Subscribe"/> listeners.
/// </summary>
public sealed class CombatPassiveEventBus
{
    private readonly List<Action<PassiveTrigger, BattleState, CombatPassiveEventContext>> _listeners = [];

    /// <summary>
    /// Barriers in (0,1]; when <see cref="RaiseDamageTaken"/> receives HP ratios, each crossed barrier emits
    /// <see cref="PassiveTrigger.HpPercentThresholdCrossed"/> (down or up across that level).
    /// </summary>
    public IList<double> MonitoredHpPercentBarriers { get; } = new List<double>();

    public void Subscribe(Action<PassiveTrigger, BattleState, CombatPassiveEventContext> listener) =>
        _listeners.Add(listener);

    public void Unsubscribe(Action<PassiveTrigger, BattleState, CombatPassiveEventContext> listener) =>
        _listeners.Remove(listener);

    public void ClearSubscribers() => _listeners.Clear();

    private void Dispatch(PassiveTrigger trigger, BattleState state, CombatPassiveEventContext context)
    {
        foreach (var listener in _listeners)
        {
            listener(trigger, state, context);
        }
    }

    public void RaiseTurnStarted(BattleState state, Combatant actor, Action<TokenType, int>? onTokenGranted)
    {
        actor.PassiveRuntime.BeginTurn();
        PassiveRuleApplier.ApplyTurnStartPassives(state, actor, onTokenGranted);
        PassiveDataDrivenEngine.HandleActivation(
            state,
            actor,
            PassiveActivationKind.OnTurnStart,
            new CombatPassiveEventContext { Self = actor });
        Dispatch(
            PassiveTrigger.TurnStarted,
            state,
            new CombatPassiveEventContext { Self = actor });
    }

    public void RaiseTurnEnded(BattleState state, Combatant actor)
    {
        var context = new CombatPassiveEventContext { Self = actor };
        PassiveDataDrivenEngine.HandleActivation(state, actor, PassiveActivationKind.OnTurnEnd, context);
        Dispatch(
            PassiveTrigger.TurnEnded,
            state,
            context);
    }

    public double GetDotTickDamageMultiplier(BattleState state, Combatant victim, DotInstance dot)
    {
        var mult = PassiveRuleApplier.GetDotTickDamageMultiplier(state, victim, dot);
        Dispatch(
            PassiveTrigger.BeforeDotTickDamage,
            state,
            new CombatPassiveEventContext
            {
                Self = victim,
                Dot = dot,
                Other = state.GetAllCombatants().FirstOrDefault(combatant => combatant.Identity.Id == dot.AppliedById),
            });
        return mult;
    }

    public (DamageModifierAccumulator Acc, bool ConsumeImpeto, List<PassiveCombatNote> OutNotes) AccumulateDamageCausedModifiers(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        bool notifyObservers = true,
        List<PassiveCombatNote>? noteSink = null)
    {
        var notes = noteSink ?? new List<PassiveCombatNote>();
        var result = PassiveRuleApplier.AccumulateDamageCausedModifiers(state, actor, target, skill, notes);
        if (notifyObservers)
        {
            Dispatch(
                PassiveTrigger.BeforeOutgoingDamage,
                state,
                new CombatPassiveEventContext { Self = actor, Other = target, Skill = skill });
        }

        return (result.Acc, result.ConsumeImpeto, notes);
    }

    public (double Mult, List<PassiveCombatNote> OutNotes) AccumulateIncomingDamageMultiplier(
        BattleState state,
        Combatant defender,
        bool notifyObservers = true,
        List<PassiveCombatNote>? noteSink = null)
    {
        var notes = noteSink ?? new List<PassiveCombatNote>();
        var incoming = PassiveRuleApplier.AccumulateIncomingDamageMultiplier(state, defender, notes);
        if (notifyObservers)
        {
            Dispatch(
                PassiveTrigger.BeforeIncomingDamage,
                state,
                new CombatPassiveEventContext { Self = defender });
        }

        return (incoming.Mult, notes);
    }

    public void RaiseOutgoingHitSuccess(
        BattleState state,
        Combatant actor,
        Combatant? hitTarget,
        SkillDefinition skill,
        bool hit,
        bool wasCrit = false)
    {
        PassiveRuleApplier.OnOutgoingHitSuccess(state, actor, skill, hit);
        if (hit)
        {
            var context = new CombatPassiveEventContext
            {
                Self = actor,
                Other = hitTarget,
                Skill = skill,
                WasCrit = wasCrit,
            };
            if (wasCrit)
            {
                PassiveDataDrivenEngine.HandleActivation(
                    state,
                    actor,
                    PassiveActivationKind.UponCriticalStrike,
                    context);
            }

            PassiveDataDrivenEngine.HandleActivation(
                state,
                actor,
                PassiveActivationKind.UponHittingTargetWithStatus,
                context);
            Dispatch(
                PassiveTrigger.AfterOutgoingHitResolved,
                state,
                context);
        }
    }

    public int AdjustDotDuration(BattleState state, Combatant actor, DotType dotType, int baseDuration) =>
        PassiveRuleApplier.AdjustDotDuration(state, actor, dotType, baseDuration);

    public void ApplyPassiveExtraDotsAfterEnemySkill(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        double elementalDamageMultiplier,
        Func<Combatant, DotType, bool> dotApplicationPassesResistanceCheck,
        List<PassiveCombatNote>? narrativeNotes = null) =>
        PassiveRuleApplier.ApplyPassiveExtraDotsAfterEnemySkill(
            state,
            actor,
            target,
            skill,
            elementalDamageMultiplier,
            dotApplicationPassesResistanceCheck,
            narrativeNotes);

    public void ApplyPostSkillPassiveExtras(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        List<PassiveCombatNote>? narrativeNotes = null)
    {
        PassiveRuleApplier.ApplyPostSkillPassiveExtras(
            state,
            actor,
            target,
            skill,
            onExtraTokenGranted: (recipient, tokenType, delta) =>
                RaiseTokenStacksChanged(state, actor, recipient, skill, tokenType, delta),
            narrativeNotes);
    }

    /// <summary>After on-hit effects + passive extra DOTs; central point for "skill fully applied".</summary>
    public void RaiseAfterSkillResolved(BattleState state, Combatant actor, Combatant target, SkillDefinition skill)
    {
        Dispatch(
            PassiveTrigger.AfterSkillEffectsResolved,
            state,
            new CombatPassiveEventContext { Self = actor, Other = target, Skill = skill });
    }

    /// <summary>Player-chosen skill started resolving (not follow-up invocations).</summary>
    public void RaiseSkillUsed(BattleState state, Combatant actor, Combatant? selectedTarget, SkillDefinition skill)
    {
        PassiveDataDrivenEngine.HandleActivation(
            state,
            actor,
            PassiveActivationKind.UponUsingSkill,
            new CombatPassiveEventContext
            {
                Self = actor,
                Other = selectedTarget,
                Skill = skill,
            });
    }

    public void RaiseDamageTaken(
        BattleState state,
        Combatant? attacker,
        Combatant defender,
        SkillDefinition? skill,
        int damage,
        bool wasCrit,
        double? hpPercentBefore = null,
        double? hpPercentAfter = null)
    {
        Dispatch(
            PassiveTrigger.DamageTaken,
            state,
            new CombatPassiveEventContext
            {
                Self = defender,
                Other = attacker,
                Skill = skill,
                DamageAmount = damage,
                WasCrit = wasCrit,
                HpPercentBefore = hpPercentBefore,
                HpPercentAfter = hpPercentAfter,
            });

        PassiveDataDrivenEngine.RegisterDamageTakenTowardEveryXHitPoints(defender, damage);
        var defenderContext = new CombatPassiveEventContext
        {
            Self = defender,
            Other = attacker,
            Skill = skill,
            DamageAmount = damage,
            WasCrit = wasCrit,
            HpPercentBefore = hpPercentBefore,
            HpPercentAfter = hpPercentAfter,
        };
        PassiveDataDrivenEngine.HandleActivation(
            state,
            defender,
            PassiveActivationKind.UponDamageTaken,
            defenderContext);
        PassiveDataDrivenEngine.HandleActivation(
            state,
            defender,
            PassiveActivationKind.UponStatThreshold,
            defenderContext);
        NotifySameSideAllies(
            state,
            defender,
            PassiveActivationKind.UponAllyDamageTaken,
            new CombatPassiveEventContext
            {
                Self = defender,
                Other = attacker,
                Victim = defender,
                Skill = skill,
                DamageAmount = damage,
                WasCrit = wasCrit,
                HpPercentBefore = hpPercentBefore,
                HpPercentAfter = hpPercentAfter,
            });

        if (attacker != null &&
            !string.Equals(attacker.Identity.Id, defender.Identity.Id, StringComparison.Ordinal))
        {
            var attackerContext = new CombatPassiveEventContext
            {
                Self = attacker,
                Other = defender,
                Skill = skill,
                DamageAmount = damage,
                WasCrit = wasCrit,
            };
            PassiveDataDrivenEngine.HandleActivation(
                state,
                attacker,
                PassiveActivationKind.UponDealingDamage,
                attackerContext);
            NotifySameSideAllies(
                state,
                attacker,
                PassiveActivationKind.UponAllyDealingDamage,
                new CombatPassiveEventContext
                {
                    Self = attacker,
                    Other = defender,
                    Killer = attacker,
                    Skill = skill,
                    DamageAmount = damage,
                    WasCrit = wasCrit,
                });
        }

        if (hpPercentBefore is not null &&
            hpPercentAfter is not null &&
            MonitoredHpPercentBarriers.Count > 0)
        {
            RaiseHpPercentThresholdCrossed(state, defender, hpPercentBefore.Value, hpPercentAfter.Value);
        }
    }

    public void RaiseTokenStacksChanged(
        BattleState state,
        Combatant sourceActor,
        Combatant recipient,
        SkillDefinition? skill,
        TokenType tokenType,
        int delta)
    {
        var contextBase = new CombatPassiveEventContext
        {
            Self = recipient,
            Other = sourceActor,
            Skill = skill,
            TokenType = tokenType,
            TokenDelta = delta,
        };

        Dispatch(PassiveTrigger.TokenStacksChanged, state, contextBase);

        if (delta > 0)
        {
            var selfApply = ReferenceEquals(recipient, sourceActor);
            Dispatch(
                selfApply ? PassiveTrigger.TokenAppliedToSelf : PassiveTrigger.TokenAppliedToOther,
                state,
                contextBase);

            PassiveDataDrivenEngine.HandleActivation(
                state,
                recipient,
                PassiveActivationKind.UponReceivingStatus,
                contextBase);
            var applierContext = new CombatPassiveEventContext
            {
                Self = sourceActor,
                Other = recipient,
                Skill = skill,
                TokenType = tokenType,
                TokenDelta = delta,
            };
            PassiveDataDrivenEngine.HandleActivation(
                state,
                sourceActor,
                PassiveActivationKind.UponApplyingStatus,
                applierContext);
        }
    }

    public void RaiseHealingDealt(
        BattleState state,
        Combatant healer,
        Combatant recipient,
        SkillDefinition? skill,
        int healAmount)
    {
        if (healAmount <= 0)
        {
            return;
        }

        var context = new CombatPassiveEventContext
        {
            Self = healer,
            Other = recipient,
            Skill = skill,
            HealAmount = healAmount,
        };
        Dispatch(PassiveTrigger.HealingDealt, state, context);
        PassiveDataDrivenEngine.HandleActivation(
            state,
            healer,
            PassiveActivationKind.UponHealing,
            context);
    }

    public void RaiseCombatantSlain(BattleState state, Combatant? killer, Combatant victim)
    {
        Dispatch(
            PassiveTrigger.CombatantSlain,
            state,
            new CombatPassiveEventContext { Killer = killer, Victim = victim });
        if (killer != null)
        {
            if (victim.Identity.Faction == Faction.Enemy)
            {
                killer.PassiveRuntime.EnemiesDefeatedThisBattle++;
            }

            PassiveDataDrivenEngine.HandleActivation(
                state,
                killer,
                PassiveActivationKind.UponKill,
                new CombatPassiveEventContext
                {
                    Self = killer,
                    Other = victim,
                    Killer = killer,
                    Victim = victim,
                });
        }
    }

    private static void NotifySameSideAllies(
        BattleState state,
        Combatant eventCombatant,
        PassiveActivationKind activation,
        CombatPassiveEventContext context)
    {
        var roster = eventCombatant.Position.Side == Side.Allies ? state.Allies : state.Enemies;
        foreach (var ally in roster)
        {
            if (ally.Health.IsDead ||
                string.Equals(ally.Identity.Id, eventCombatant.Identity.Id, StringComparison.Ordinal))
            {
                continue;
            }

            PassiveDataDrivenEngine.HandleActivation(state, ally, activation, context);
        }
    }

    /// <summary>Emits one event per barrier in <see cref="MonitoredHpPercentBarriers"/> crossed between the two ratios.</summary>
    public void RaiseHpPercentThresholdCrossed(
        BattleState state,
        Combatant self,
        double hpPercentBefore,
        double hpPercentAfter)
    {
        foreach (var barrier in MonitoredHpPercentBarriers)
        {
            if (barrier <= 0 || barrier > 1) continue;

            var crossedDown = hpPercentBefore > barrier && hpPercentAfter <= barrier;
            var crossedUp = hpPercentBefore < barrier && hpPercentAfter >= barrier;
            if (!crossedDown && !crossedUp) continue;

            Dispatch(
                PassiveTrigger.HpPercentThresholdCrossed,
                state,
                new CombatPassiveEventContext
                {
                    Self = self,
                    HpPercentBefore = hpPercentBefore,
                    HpPercentAfter = hpPercentAfter,
                    CrossedHpPercentBarrier = barrier,
                });
        }
    }
}
