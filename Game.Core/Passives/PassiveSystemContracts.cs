namespace Game.Core.Passives;

/// <summary>
/// Tipos fechados para o MVP de passivas (expandir com versão de dados).
/// Gatilhos de execução: ver <see cref="PassiveTrigger"/> e <see cref="CombatPassiveEventBus"/>.
/// </summary>
public enum PassiveEffectKind
{
    OutgoingDamageVsSkillId = 0,
    OutgoingDamageVsDotOnTarget = 1,
    DotDurationBonus = 2,
    IncomingDamageMultiplierWhenHpBelow = 3,
    OutgoingDamagePenaltyWhenToken = 4,
    OutgoingDamageAfterPrerequisiteSkill = 5,
    ExtraTokenOnSelfSkill = 6,
    ExtraHealPercentOnSelfSkill = 7,
    ApplyExtraDotAfterSkillIfTargetHasDot = 8,
    OutgoingDamageVsSkillIfTargetHasDot = 9,
    DotTickDamageBonusWhenTargetHpBelow = 10,
    GrantTokenAtTurnStartIfCondition = 11,
}

/// <summary>
/// Acumuladores numéricos para o pipeline de dano; o simulador lê-os após invocar as passivas.
/// </summary>
public struct DamageModifierAccumulator
{
    public double OutgoingDamageAdditiveSum;
    public double OutgoingDamageMultiplicativeProduct;
    public double IncomingDamageMultiplicativeProduct;

    public DamageModifierAccumulator()
    {
        OutgoingDamageAdditiveSum = 0;
        OutgoingDamageMultiplicativeProduct = 1.0;
        IncomingDamageMultiplicativeProduct = 1.0;
    }
}
