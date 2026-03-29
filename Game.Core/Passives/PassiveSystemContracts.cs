using Game.Core.Abstractions;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Passives;

/// <summary>
/// Pontos estáveis no fluxo de combate onde regras de passiva podem contribuir.
/// Ver <c>docs/passives-system-spec.md</c> para ordem e semântica.
/// </summary>
public enum PassiveHook
{
    TurnStartedSelf = 0,
    BeforeDotTickDamage = 1,
    AfterDotTickDamage = 2,
    BeforeOutgoingHitRoll = 3,
    BeforeOutgoingDamage = 4,
    AfterOutgoingDamagePreMitigation = 5,
    BeforeIncomingDamage = 6,
    AfterIncomingDamage = 7,
    AfterSkillEffectsResolved = 8,
}

/// <summary>
/// Tipos fechados para o MVP de passivas (expandir com versão de dados).
/// </summary>
public enum PassiveEffectKind
{
    OutgoingDamageVsSkillId = 0,
    OutgoingDamageVsDotOnTarget = 1,
    DotDurationBonus = 2,
    IncomingDamageMultiplierWhenHpBelow = 3,
    OutgoingDamagePenaltyWhenToken = 4,
    OutgoingDamageAfterPrerequisiteSkill = 5,
    ExtraTokenOnSelfSkillWhenRank = 6,
    ExtraHealPercentOnSelfSkill = 7,
    ApplyExtraDotAfterSkillIfTargetHasDot = 8,
    OutgoingDamageVsSkillIfTargetHasDot = 9,
    DotTickDamageBonusWhenTargetHpBelow = 10,
    GrantTokenAtTurnStartIfCondition = 11,
}

/// <summary>
/// Contexto imutável passado às regras; o simulador preenche por hook.
/// Todo RNG deve vir de <see cref="Random"/> (nunca Random.Shared nas regras).
/// </summary>
public sealed class PassiveEvaluationContext
{
    public required PassiveHook Hook { get; init; }
    public required BattleState State { get; init; }
    public required IRandomSource Random { get; init; }
    public required Combatant Self { get; init; }
    public Combatant? Other { get; init; }
    public SkillDefinition? Skill { get; init; }
    public int RolledBaseDamage { get; init; }
    public bool HitWasCrit { get; init; }
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

/// <summary>
/// Uma regra carregada a partir de dados + registo; implementações pequenas por <see cref="PassiveEffectKind"/>.
/// </summary>
public interface IPassiveRule
{
    string PassiveNodeId { get; }
    PassiveEffectKind Kind { get; }
    void Contribute(PassiveEvaluationContext context, ref DamageModifierAccumulator modifiers);
}
