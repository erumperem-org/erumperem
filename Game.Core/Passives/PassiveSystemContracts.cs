namespace Game.Core.Passives;

/// <summary>
/// Tipos fechados para o MVP de passivas (expandir com versão de dados).
/// Gatilhos de execução: ver <see cref="PassiveTrigger"/> e <see cref="CombatPassiveEventBus"/>.
/// </summary>
public enum PassiveEffectKind
{
    DamageCausedVsSkillId = 0,
    DamageCausedVsDotOnTarget = 1,
    DotDurationBonus = 2,
    IncomingDamageMultiplierWhenHpBelow = 3,
    DamageCausedPenaltyWhenToken = 4,
    DamageCausedAfterPrerequisiteSkill = 5,
    ExtraTokenOnSelfSkill = 6,
    ExtraHealPercentOnSelfSkill = 7,
    ApplyExtraDotAfterSkillIfTargetHasDot = 8,
    DamageCausedVsSkillIfTargetHasDot = 9,
    DotTickDamageBonusWhenTargetHpBelow = 10,
    GrantTokenAtTurnStartIfCondition = 11,
    SummonEnemyAtTurnStartWhenHpBelowTiered = 12,
}

/// <summary>
/// Acumuladores numéricos para o pipeline de dano; o simulador lê-os após invocar as passivas.
/// </summary>
public struct DamageModifierAccumulator
{
    public double DamageCausedAdditiveSum;
    public double DamageCausedMultiplicativeProduct;
    public double IncomingDamageMultiplicativeProduct;

    public DamageModifierAccumulator()
    {
        DamageCausedAdditiveSum = 0;
        DamageCausedMultiplicativeProduct = 1.0;
        IncomingDamageMultiplicativeProduct = 1.0;
    }
}
