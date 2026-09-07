using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Core.Engine;

/// <summary>
/// Single source of truth for direct damage math shared by combat resolution, UI preview, and AI estimates.
/// </summary>
public static class CombatDamageCalculator
{
    public static double GetElementalMultiplier(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        var attackElement = skill.Element == ElementType.None
            ? actor.ElementAffinity.Element
            : skill.Element;
        var defenseElement = target.ElementAffinity.Element;
        if (ElementTriangle.HasAdvantage(attackElement, defenseElement))
        {
            return state.BalanceConfig.ElementAdvantageMultiplier;
        }

        if (ElementTriangle.HasAdvantage(defenseElement, attackElement))
        {
            return state.BalanceConfig.ElementDisadvantageMultiplier;
        }

        return 1.0;
    }

    public static double CorruptionDamageMultiplier(BattleState state, Combatant actor, Combatant target)
    {
        var tierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);
        if (actor.Identity.Faction == Faction.Player)
        {
            return tierModifiers.PlayerDamageDealtMultiplier;
        }

        if (target.Identity.Faction == Faction.Player)
        {
            return tierModifiers.PlayerDamageTakenMultiplier;
        }

        return 1.0;
    }

    public static double EffectiveCritChanceFraction(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        var baseChance = skill.BaseCritChance + actor.Stats.CritChance;
        var tierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);

        if (actor.Identity.Faction == Faction.Player)
        {
            baseChance += tierModifiers.PlayerCritBonus;
        }

        if (target.Identity.Faction == Faction.Player)
        {
            baseChance += tierModifiers.EnemyCritBonusAgainstPlayer;
        }

        return Math.Clamp(baseChance, 0, 1);
    }

    public static int ApplyMitigation(
        BattleState state,
        Combatant target,
        int damage,
        bool consumeMitigationTokens)
    {
        if (target.Tokens.GetStacks(TokenType.BlockPlus) > 0)
        {
            if (consumeMitigationTokens)
            {
                target.Tokens.ConsumeOne(TokenType.BlockPlus);
            }

            damage = (int)Math.Round(damage * state.BalanceConfig.BlockPlusDamageMultiplier);
        }
        else if (target.Tokens.GetStacks(TokenType.Block) > 0)
        {
            if (consumeMitigationTokens)
            {
                target.Tokens.ConsumeOne(TokenType.Block);
            }

            damage = (int)Math.Round(damage * state.BalanceConfig.BlockDamageMultiplier);
        }

        return Math.Max(0, damage);
    }

    public readonly record struct DirectDamageBeforeMitigation(
        int DamageBeforeMitigation,
        bool ShouldClearImpetoCleaveBonus,
        IReadOnlyList<PassiveCombatNote> OutgoingPassiveNotes,
        IReadOnlyList<PassiveCombatNote> IncomingPassiveNotes);

    public static DirectDamageBeforeMitigation ComputeDirectDamageBeforeMitigation(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        double baseDamageAmount,
        bool isCriticalStrike,
        bool capturePassiveNotes)
    {
        var outgoingPassiveNotes = capturePassiveNotes ? new List<PassiveCombatNote>() : null;
        var incomingPassiveNotes = capturePassiveNotes ? new List<PassiveCombatNote>() : null;
        var notifyPassiveObservers = capturePassiveNotes;

        var damage = baseDamageAmount;
        damage *= GetElementalMultiplier(state, actor, target, skill);

        if (isCriticalStrike)
        {
            damage *= CorruptionRules.BaseCriticalStrikeDamageMultiplier;
            if (actor.Identity.Faction == Faction.Enemy &&
                target.Identity.Faction == Faction.Player)
            {
                var enemyCritTierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);
                damage *= enemyCritTierModifiers.EnemyCritDamageMultiplierAgainstPlayer;
            }
        }

        damage *= CorruptionDamageMultiplier(state, actor, target);

        var shouldClearImpetoCleaveBonus = false;
        if (damage > 0 && target.Identity.Id != actor.Identity.Id)
        {
            var (outgoingAccumulator, consumeImpeto, _) =
                state.PassiveBus.AccumulateOutgoingDamageModifiers(
                    state,
                    actor,
                    target,
                    skill,
                    notifyObservers: notifyPassiveObservers,
                    noteSink: outgoingPassiveNotes);
            damage *= (1.0 + outgoingAccumulator.OutgoingDamageAdditiveSum) *
                      outgoingAccumulator.OutgoingDamageMultiplicativeProduct;
            damage = Math.Max(0, damage);
            shouldClearImpetoCleaveBonus = consumeImpeto;
        }

        if (damage > 0)
        {
            var (incomingMultiplier, _) =
                state.PassiveBus.AccumulateIncomingDamageMultiplier(
                    state,
                    target,
                    notifyObservers: notifyPassiveObservers,
                    noteSink: incomingPassiveNotes);
            damage *= incomingMultiplier;
            damage = Math.Max(0, damage);
        }

        if (damage > 0 &&
            actor.Identity.Faction == Faction.Player &&
            state.AllyOutgoingDamageMultiplier > 0 &&
            Math.Abs(state.AllyOutgoingDamageMultiplier - 1.0) > double.Epsilon)
        {
            damage *= state.AllyOutgoingDamageMultiplier;
            damage = Math.Max(0, damage);
        }

        IReadOnlyList<PassiveCombatNote> resolvedOutgoingNotes = outgoingPassiveNotes != null
            ? outgoingPassiveNotes
            : Array.Empty<PassiveCombatNote>();
        IReadOnlyList<PassiveCombatNote> resolvedIncomingNotes = incomingPassiveNotes != null
            ? incomingPassiveNotes
            : Array.Empty<PassiveCombatNote>();

        return new DirectDamageBeforeMitigation(
            (int)Math.Round(damage),
            shouldClearImpetoCleaveBonus,
            resolvedOutgoingNotes,
            resolvedIncomingNotes);
    }

    public static int ComputeDirectDamageOnHit(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        int baseRollDamage,
        bool isCriticalStrike,
        bool consumeMitigationTokens)
    {
        var damageBeforeMitigation = ComputeDirectDamageBeforeMitigation(
            state,
            actor,
            target,
            skill,
            baseRollDamage,
            isCriticalStrike,
            capturePassiveNotes: false);
        return ApplyMitigation(
            state,
            target,
            damageBeforeMitigation.DamageBeforeMitigation,
            consumeMitigationTokens);
    }

    public static int EstimateAverageDirectDamageOnHit(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        bool consumeMitigationTokens = false)
    {
        var averageBaseDamage = (skill.BaseDamage.Min + skill.BaseDamage.Max) / 2.0;
        var damageBeforeMitigation = ComputeDirectDamageBeforeMitigation(
            state,
            actor,
            target,
            skill,
            averageBaseDamage,
            isCriticalStrike: false,
            capturePassiveNotes: false);
        return ApplyMitigation(
            state,
            target,
            damageBeforeMitigation.DamageBeforeMitigation,
            consumeMitigationTokens);
    }
}
