using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Engine;

/// <summary>
/// Espelha o pipeline de <see cref="BattleSimulator"/> para min/max em UI,
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
        CombatDamageCalculator.EffectiveCritChanceFraction(state, actor, target, skill);

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

        var minDamageOnHit = CombatDamageCalculator.ComputeDirectDamageOnHit(
            state,
            actor,
            target,
            skill,
            skill.BaseDamage.Min,
            isCriticalStrike: false,
            consumeMitigationTokens: false);
        var maxDamageOnHit = CombatDamageCalculator.ComputeDirectDamageOnHit(
            state,
            actor,
            target,
            skill,
            skill.BaseDamage.Max,
            isCriticalStrike: CombatDamageCalculator.EffectiveCritChanceFraction(state, actor, target, skill) > 0,
            consumeMitigationTokens: false);

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
}
