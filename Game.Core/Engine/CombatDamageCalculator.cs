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
        var actorModifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(state, actor, target, skill);
        var baseChance = skill.BaseCritChance + actor.Stats.CritChance + actorModifiers.CritChanceAdditive;
        var tierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);

        if (actor.Identity.Faction == Faction.Player)
        {
            baseChance += tierModifiers.PlayerCritBonus;
        }

        if (target.Identity.Faction == Faction.Player)
        {
            baseChance += tierModifiers.EnemyCritBonusAgainstPlayer;
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
        damage *= CombatStatusRules.IncomingDamageMultiplierFromTokens(target.Tokens, targetTokenEfficiency);
        damage = CombatStatusRules.ApplyBaseDefenseChance(
            damage,
            target.Stats.DefenseChance + defenderModifiers.DefenseChanceAdditive);

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

        IReadOnlyList<PassiveCombatNote> resolvedOutgoingNotes = outgoingPassiveNotes != null
            ? outgoingPassiveNotes
            : Array.Empty<PassiveCombatNote>();
        IReadOnlyList<PassiveCombatNote> resolvedIncomingNotes = incomingPassiveNotes != null
            ? incomingPassiveNotes
            : Array.Empty<PassiveCombatNote>();

        return new DirectDamageBeforeMitigation(
            FloorConnectedHitDamage((int)Math.Round(damage), damage),
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
        var actorModifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(state, actor, target, skill);
        var actorTokenEfficiency = PassiveDataDrivenEngine.CreateTokenEfficiencyLookup(state, actor, target);
        var targetTokenEfficiency = PassiveDataDrivenEngine.CreateTokenEfficiencyLookup(state, target, actor);
        var hitChance = skill.Accuracy * actor.Stats.Accuracy;
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

        if (actor.Identity.Faction == Faction.Enemy)
        {
            var enemyAccuracyTierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);
            hitChance += enemyAccuracyTierModifiers.EnemyAccuracyBonus;
        }

        if (skill.Accuracy <= 0 && hitChance <= 0)
        {
            return Math.Max(0, hitChance);
        }

        return Math.Clamp(
            hitChance,
            CombatStatusRules.MinimumHitChanceFraction,
            CombatStatusRules.MaximumHitChanceFraction);
    }

    public static int FloorConnectedHitDamage(int roundedDamage, double unroundedDamage)
    {
        if (unroundedDamage > 0)
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
