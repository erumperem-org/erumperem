using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Engine;

/// <summary>
/// Single source of truth for direct damage math shared by combat resolution, UI preview, and AI estimates.
/// </summary>
public static class CombatDamageCalculator
{
    public const double MaxDamageReduction = 0.90; // Redução máxima de dano de 90%

    public static double GetElementalMultiplier(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        var attackElement = ResolveAttackElement(actor, skill);
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

    public static ElementType ResolveAttackElement(Combatant actor, SkillDefinition skill)
    {
        if (skill == null || actor == null)
        {
            return ElementType.None;
        }

        return skill.Element == ElementType.None
            ? actor.ElementAffinity.Element
            : skill.Element;
    }

    public static ElementMatchupKind GetElementMatchup(
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        var attackElement = ResolveAttackElement(actor, skill);
        var defenseElement = target?.ElementAffinity.Element ?? ElementType.None;
        return ElementTriangle.GetMatchup(attackElement, defenseElement);
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
        var isEnemy = actor.Identity.Faction == Faction.Enemy || actor.Position?.Side == Side.Enemies;
        var tierModifiers = state?.BalanceConfig?.GetTierModifiers(state.CorruptionTier);

        // REGRA: Last tier: enemies Always crit
        if (isEnemy && (tierModifiers?.EnemiesAlwaysCrit == true || state?.CorruptionTier >= 4))
        {
            return 1.0;
        }

        var actorModifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(state, actor, target, skill);
        var baseChance = skill.BaseCritChance + actor.Stats.CritChance + actorModifiers.CritChanceAdditive;

        // REGRA: every tier: +5% critical to all skills for players and enemies
        if (tierModifiers != null)
        {
            baseChance += tierModifiers.GlobalCritBonus;
        }
        else if (state != null)
        {
            baseChance += state.CorruptionTier * 0.05;
        }

        baseChance += CombatStatusRules.CritChanceBonusFromAttackerTokens(actor.Tokens);
        baseChance += CombatStatusRules.CritChanceBonusFromDefenderTokens(
            target.Tokens,
            PassiveDataDrivenEngine.CreateTokenEfficiencyLookup(state, target, actor));

        if (skill.ComputeFromDebuffTypesOnTarget)
        {
            var distinctDebuffCount = CombatStatusRules.CountDistinctDebuffTypes(target.Tokens);
            baseChance += skill.CritChancePerDistinctDebuffType * distinctDebuffCount;
        }

        return Math.Clamp(baseChance, 0, 1);
    }

    public static int ApplyMitigation(
        BattleState state,
        Combatant target,
        int damage,
        bool consumeMitigationTokens)
    {
        _ = state;
        _ = target;
        _ = consumeMitigationTokens;
        return Math.Max(0, damage);
    }

    public static int ApplyMitigationForConnectedHit(
        BattleState state,
        Combatant target,
        int damageBeforeMitigation,
        bool consumeMitigationTokens)
    {
        var damageAfterMitigation = ApplyMitigation(
            state,
            target,
            damageBeforeMitigation,
            consumeMitigationTokens);

        // REGRA: on hit, if dmg < 1; dmg becomes 1
        if (damageBeforeMitigation > 0 && damageAfterMitigation < 1)
        {
            damageAfterMitigation = 1;
        }

        return FloorConnectedHitDamage(damageAfterMitigation, damageBeforeMitigation);
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

        var actorModifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(state, actor, target, skill);
        var defenderModifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(state, target, actor, skill);
        var actorTokenEfficiency = PassiveDataDrivenEngine.CreateTokenEfficiencyLookup(state, actor, target);
        var targetTokenEfficiency = PassiveDataDrivenEngine.CreateTokenEfficiencyLookup(state, target, actor);

        var damage = baseDamageAmount + actorModifiers.SkillDamageFlat;
        damage *= GetElementalMultiplier(state, actor, target, skill);

        if (isCriticalStrike)
        {
            damage *= CombatStatusRules.CritDamageMultiplierFromDefenderMark(target.Tokens, targetTokenEfficiency);
            damage *= 1.0 + actorModifiers.CritDamageAdditive;
            if (actor.Identity.Faction == Faction.Enemy &&
                target.Identity.Faction == Faction.Player)
            {
                var enemyCritTierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);
                damage *= enemyCritTierModifiers.EnemyCritDamageMultiplierAgainstPlayer;
            }
        }

        damage *= CorruptionDamageMultiplier(state, actor, target);
        damage *= CombatStatusRules.DamageCausedMultiplierFromTokens(actor.Tokens, actorTokenEfficiency);

        // Multiplicador defensivo de tokens
        damage *= CombatStatusRules.IncomingDamageMultiplierFromTokens(target.Tokens, targetTokenEfficiency);

        // REGRA: maximum damage reduction = 90% (clamp da defesa base entre 0.0 e 0.90)
        double defenseChance = target.Stats.DefenseChance + defenderModifiers.DefenseChanceAdditive;
        double clampedDefenseChance = Math.Clamp(defenseChance, 0.0, MaxDamageReduction);
        damage = CombatStatusRules.ApplyBaseDefenseChance(damage, clampedDefenseChance);

        var shouldClearImpetoCleaveBonus = false;
        if (damage > 0 && target.Identity.Id != actor.Identity.Id)
        {
            var (damageCausedAccumulator, consumeImpeto, _) =
                state.PassiveBus.AccumulateDamageCausedModifiers(
                    state,
                    actor,
                    target,
                    skill,
                    notifyObservers: notifyPassiveObservers,
                    noteSink: outgoingPassiveNotes);
            damage *= (1.0 + damageCausedAccumulator.DamageCausedAdditiveSum + actorModifiers.DamageCausedAdditive) *
                      damageCausedAccumulator.DamageCausedMultiplicativeProduct;
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

        // REGRA: on hit, if dmg < 1; dmg becomes 1 (se a skill possui dano base)
        if (baseDamageAmount > 0 && damage < 1.0)
        {
            damage = 1.0;
        }

        IReadOnlyList<PassiveCombatNote> resolvedOutgoingNotes = outgoingPassiveNotes != null
            ? outgoingPassiveNotes
            : Array.Empty<PassiveCombatNote>();
        IReadOnlyList<PassiveCombatNote> resolvedIncomingNotes = incomingPassiveNotes != null
            ? incomingPassiveNotes
            : Array.Empty<PassiveCombatNote>();

        int roundedDamage = (int)Math.Round(damage);
        if (baseDamageAmount > 0 && roundedDamage < 1)
        {
            roundedDamage = 1;
        }

        return new DirectDamageBeforeMitigation(
            FloorConnectedHitDamage(roundedDamage, damage),
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
        return ApplyMitigationForConnectedHit(
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
        return ApplyMitigationForConnectedHit(
            state,
            target,
            damageBeforeMitigation.DamageBeforeMitigation,
            consumeMitigationTokens);
    }

    public static double ComputeEffectiveHitChanceFraction(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        var isEnemy = actor.Identity.Faction == Faction.Enemy || actor.Position?.Side == Side.Enemies;
        var tierModifiers = state?.BalanceConfig?.GetTierModifiers(state.CorruptionTier);

        // REGRA: Last tier: enemies have guaranteed hits
        if (isEnemy && (tierModifiers?.EnemiesGuaranteedHit == true || state?.CorruptionTier >= 4))
        {
            return 1.0;
        }

        var actorModifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(state, actor, target, skill);
        var actorTokenEfficiency = PassiveDataDrivenEngine.CreateTokenEfficiencyLookup(state, actor, target);
        var targetTokenEfficiency = PassiveDataDrivenEngine.CreateTokenEfficiencyLookup(state, target, actor);
        var baseHitChance = skill.Accuracy * actor.Stats.Accuracy;
        var hitChance = baseHitChance;
        hitChance += actorModifiers.AccuracyAdditive + actorModifiers.SkillAccuracyAdditive;
        hitChance += CombatStatusRules.AccuracyModifierFromActorTokens(actor.Tokens, actorTokenEfficiency);
        hitChance += CombatStatusRules.AccuracyBonusFromTargetExposition(target.Tokens, targetTokenEfficiency);
        hitChance -= CombatStatusRules.AccuracyPenaltyFromTargetStealth(target.Tokens);

        if (skill.AccuracyPenaltyPerLivingEnemy > 0)
        {
            var livingEnemyCount = state.GetAllCombatants().Count(combatant =>
                !combatant.Health.IsDead &&
                combatant.Position.Side != actor.Position.Side);
            hitChance -= skill.AccuracyPenaltyPerLivingEnemy * livingEnemyCount;
        }

        if (skill.ComputeFromDebuffTypesOnTarget)
        {
            var distinctDebuffCount = CombatStatusRules.CountDistinctDebuffTypes(target.Tokens);
            hitChance += skill.AccuracyPerDistinctDebuffType * distinctDebuffCount;
        }

        // REGRA: every tier: +10% base accuracy to all skills for players and enemies
        if (tierModifiers != null)
        {
            hitChance += tierModifiers.GlobalAccuracyBonus;
        }
        else if (state != null)
        {
            hitChance += state.CorruptionTier * 0.10;
        }

        if (skill.Accuracy <= 0 && hitChance <= 0)
        {
            return Math.Max(0, hitChance);
        }

        var minimumHitChance = CombatStatusRules.ComputeMinimumHitChance(baseHitChance);
        return Math.Clamp(
            hitChance,
            minimumHitChance,
            CombatStatusRules.MaximumHitChanceFraction);
    }

    public static int FloorConnectedHitDamage(int roundedDamage, double unroundedDamage)
    {
        // REGRA: on hit, if dmg < 1; dmg becomes 1 (para ataques conectados com dano)
        if (unroundedDamage > 0 || roundedDamage > 0)
        {
            return Math.Max(1, roundedDamage);
        }

        return Math.Max(0, roundedDamage);
    }

    public static int ComputeBonusDamageFromSkillTokens(
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        var bonusDamage = 0;
        if (skill.BonusDamagePerOwnToken.HasValue)
        {
            var ownTokenStacks = actor.Tokens.GetStacks(skill.BonusDamagePerOwnToken.Value);
            bonusDamage += ownTokenStacks * Math.Max(1, skill.BonusDamagePerOwnTokenStacks);
        }

        if (skill.ComputeFromDebuffTypesOnTarget)
        {
            var distinctDebuffCount = CombatStatusRules.CountDistinctDebuffTypes(target.Tokens);
            bonusDamage += distinctDebuffCount * Math.Max(0, skill.DamagePerDistinctDebuffType);
        }

        return bonusDamage;
    }
}