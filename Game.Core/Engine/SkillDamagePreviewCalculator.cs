using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Engine;

/// <summary>
/// Espelha o pipeline de <see cref="BattleSimulator.ResolveHitAndDamage"/> para min/max em UI,
/// sem consumir tokens nem alterar HP.
/// </summary>
public static class SkillDamagePreviewCalculator
{
    public static bool HasDirectDamage(SkillDefinition skill) =>
        skill.BaseDamage.Min > 0 || skill.BaseDamage.Max > 0;

    public static double ComputeEffectiveCriticalChanceFraction(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill) =>
        EffectiveCritChance(state, actor, target, skill);

    public static bool TryCompute(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        out SkillDamagePreview preview)
    {
        preview = default;
        if (state == null || actor == null || target == null || skill == null)
        {
            return false;
        }

        if (!HasDirectDamage(skill))
        {
            return false;
        }

        var minDamageOnHit = ComputeDamageOnHit(state, actor, target, skill, skill.BaseDamage.Min, forceCriticalStrike: false);
        var maxDamageOnHit = ComputeDamageOnHit(
            state,
            actor,
            target,
            skill,
            skill.BaseDamage.Max,
            forceCriticalStrike: EffectiveCritChance(state, actor, target, skill) > 0);

        var currentHp = target.Health.CurrentHp;
        var minHpAfterHit = Math.Max(0, currentHp - maxDamageOnHit);
        var maxHpAfterHit = Math.Max(0, currentHp - minDamageOnHit);
        var isGuaranteedKillOnHit = minDamageOnHit >= currentHp && currentHp > 0;

        preview = new SkillDamagePreview
        {
            MinDamageOnHit = minDamageOnHit,
            MaxDamageOnHit = maxDamageOnHit,
            MinHpAfterHit = minHpAfterHit,
            MaxHpAfterHit = maxHpAfterHit,
            IsGuaranteedKillOnHit = isGuaranteedKillOnHit,
            HitChanceFraction = ComputeHitChanceFraction(state, actor, target, skill),
        };

        return true;
    }

    private static int ComputeDamageOnHit(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill,
        int baseRollDamage,
        bool forceCriticalStrike)
    {
        var damage = baseRollDamage;
        var elementalMultiplier = GetElementalMultiplier(state, actor, target, skill);
        damage = (int)Math.Round(damage * elementalMultiplier);

        if (forceCriticalStrike)
        {
            damage = (int)Math.Round(damage * CorruptionRules.BaseCriticalStrikeDamageMultiplier);
            if (actor.Identity.Faction == Faction.Enemy &&
                target.Identity.Faction == Faction.Player)
            {
                var enemyCritTierModifiers = state.BalanceConfig.GetTierModifiers(state.CorruptionTier);
                damage = (int)Math.Round(
                    damage * enemyCritTierModifiers.EnemyCritDamageMultiplierAgainstPlayer);
            }
        }

        damage = (int)Math.Round(damage * CorruptionDamageMultiplier(state, actor, target));

        if (damage > 0 && target.Identity.Id != actor.Identity.Id)
        {
            var (outgoingAccumulator, _, _) = state.PassiveBus.AccumulateOutgoingDamageModifiers(
                state,
                actor,
                target,
                skill,
                notifyObservers: false);
            damage = (int)Math.Round(
                damage * (1.0 + outgoingAccumulator.OutgoingDamageAdditiveSum) *
                outgoingAccumulator.OutgoingDamageMultiplicativeProduct);
            damage = Math.Max(0, damage);
        }

        if (damage > 0)
        {
            var (incomingMultiplier, _) =
                state.PassiveBus.AccumulateIncomingDamageMultiplier(state, target, notifyObservers: false);
            damage = (int)Math.Round(damage * incomingMultiplier);
            damage = Math.Max(0, damage);
        }

        return PreviewApplyMitigation(state, target, damage);
    }

    private static int PreviewApplyMitigation(BattleState state, Combatant target, int damage)
    {
        if (target.Tokens.GetStacks(TokenType.BlockPlus) > 0)
        {
            damage = (int)Math.Round(damage * state.BalanceConfig.BlockPlusDamageMultiplier);
        }
        else if (target.Tokens.GetStacks(TokenType.Block) > 0)
        {
            damage = (int)Math.Round(damage * state.BalanceConfig.BlockDamageMultiplier);
        }

        return Math.Max(0, damage);
    }

    private static double ComputeHitChanceFraction(
        BattleState state,
        Combatant actor,
        Combatant target,
        SkillDefinition skill)
    {
        var hitChance = 1.0;

        if (actor.Tokens.GetStacks(TokenType.Blind) > 0)
        {
            hitChance *= 1.0 - state.BalanceConfig.BlindMissChance;
        }

        hitChance *= skill.Accuracy * actor.Stats.Accuracy;

        if (target.Tokens.GetStacks(TokenType.Dodge) > 0)
        {
            hitChance *= 1.0 - state.BalanceConfig.DodgeNegateChance;
        }

        return Math.Clamp(hitChance, 0, 1);
    }

    private static double CorruptionDamageMultiplier(BattleState state, Combatant actor, Combatant target)
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

    private static double EffectiveCritChance(
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

    private static double GetElementalMultiplier(
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
}
