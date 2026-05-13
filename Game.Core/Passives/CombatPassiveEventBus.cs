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
        PassiveRuleApplier.ApplyTurnStartPassives(state, actor, onTokenGranted);
        Dispatch(
            PassiveTrigger.TurnStarted,
            state,
            new CombatPassiveEventContext { Self = actor });
    }

    public void RaiseTurnEnded(BattleState state, Combatant actor)
    {
        Dispatch(
            PassiveTrigger.TurnEnded,
            state,
            new CombatPassiveEventContext { Self = actor });
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

    public (DamageModifierAccumulator Acc, bool ConsumeImpeto, List<PassiveCombatNote> OutNotes) AccumulateOutgoingDamageModifiers(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        bool notifyObservers = true,
        List<PassiveCombatNote>? noteSink = null)
    {
        var notes = noteSink ?? new List<PassiveCombatNote>();
        var result = PassiveRuleApplier.AccumulateOutgoingDamageModifiers(state, actor, target, skill, notes);
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
        bool hit)
    {
        PassiveRuleApplier.OnOutgoingHitSuccess(state, actor, skill, hit);
        if (hit)
        {
            Dispatch(
                PassiveTrigger.AfterOutgoingHitResolved,
                state,
                new CombatPassiveEventContext { Self = actor, Other = hitTarget, Skill = skill });
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
        }
    }

    public void RaiseComboBonusEffectsIncluded(BattleState state, Combatant actor, Combatant target, SkillDefinition skill)
    {
        Dispatch(
            PassiveTrigger.ComboBonusEffectsIncluded,
            state,
            new CombatPassiveEventContext { Self = actor, Other = target, Skill = skill });
    }

    public void RaiseComboConsumed(BattleState state, Combatant actor, Combatant target, SkillDefinition skill)
    {
        Dispatch(
            PassiveTrigger.ComboConsumed,
            state,
            new CombatPassiveEventContext { Self = target, Other = actor, Skill = skill });
    }

    public void RaiseCombatantSlain(BattleState state, Combatant? killer, Combatant victim)
    {
        Dispatch(
            PassiveTrigger.CombatantSlain,
            state,
            new CombatPassiveEventContext { Killer = killer, Victim = victim });
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
