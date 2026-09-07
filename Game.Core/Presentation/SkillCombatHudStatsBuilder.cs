using Game.Core.Engine;
using Game.Core.Models;

namespace Game.Core.Presentation;

public readonly struct SkillCombatHudStats
{
    public required int TargetCount { get; init; }
    public required bool HasDirectDamage { get; init; }
    public required int DamageMin { get; init; }
    public required int DamageMax { get; init; }
    public required double CriticalChanceFraction { get; init; }
    public required double CorruptionCost { get; init; }
}

/// <summary>
/// Valores numéricos da HUD de combate (TGT, DMG, CRT, CORR) para uma skill em contexto.
/// </summary>
public static class SkillCombatHudStatsBuilder
{
    public static SkillCombatHudStats Build(
        BattleState battleState,
        Combatant actor,
        SkillDefinition skill,
        Combatant previewTargetOrNull)
    {
        var targetCount = CountSkillTargets(battleState, actor, skill, previewTargetOrNull);
        var hasDirectDamage = SkillDamagePreviewCalculator.HasDirectDamage(skill);
        var damageMin = skill.BaseDamage.Min;
        var damageMax = skill.BaseDamage.Max;

        if (hasDirectDamage &&
            previewTargetOrNull != null &&
            SkillDamagePreviewCalculator.TryCompute(
                battleState,
                actor,
                previewTargetOrNull,
                skill,
                out var damagePreview))
        {
            damageMin = damagePreview.MinDamageOnHit;
            damageMax = damagePreview.MaxDamageOnHit;
        }

        var criticalTarget = previewTargetOrNull ?? actor;
        var criticalChanceFraction = SkillDamagePreviewCalculator.ComputeEffectiveCriticalChanceFraction(
            battleState,
            actor,
            criticalTarget,
            skill);

        return new SkillCombatHudStats
        {
            TargetCount = targetCount,
            HasDirectDamage = hasDirectDamage,
            DamageMin = damageMin,
            DamageMax = damageMax,
            CriticalChanceFraction = criticalChanceFraction,
            CorruptionCost = skill.CorruptionCost,
        };
    }

    public static int CountSkillTargets(
        BattleState battleState,
        Combatant actor,
        SkillDefinition skill,
        Combatant? previewTargetOrNull = null)
    {
        var affectedCombatantIds = SkillCombatTargetPreviewResolver.ResolveAffectedCombatantIds(
            battleState,
            actor,
            skill,
            previewTargetOrNull);
        if (affectedCombatantIds.Count > 0)
        {
            return affectedCombatantIds.Count;
        }

        return SkillTargetResolver.EstimatePrimaryTargetCount(battleState, actor, skill);
    }
}
