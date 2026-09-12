using Game.Core.Domain;
using Game.Core.Passives;

namespace Game.Core.Models;

/// <summary>
/// Definição carregada de JSON; o <see cref="Id"/> deve coincidir com um nó <c>Passive</c> em <c>skill_trees.json</c>.
/// </summary>
public sealed class PassiveDefinition
{
    public required string Id { get; init; }
    public PassiveEffectKind EffectKind { get; init; }
    public string? SkillId { get; init; }
    public string? PrerequisiteSkillId { get; init; }
    public DotType? DotType { get; init; }
    public TokenType? TokenType { get; init; }
    public TokenType? GrantTokenType { get; init; }
    public TokenType? IfHasTokenType { get; init; }
    public TokenType? UnlessHasTokenType { get; init; }

    /// <summary>Bónus aditivo ao multiplicador de dano (ex.: 0.10 = +10%).</summary>
    public double Additive { get; init; }

    public double AdditivePerStack { get; init; }
    public double Cap { get; init; }
    public double HpBelowPercent { get; init; }
    public int IntValue { get; init; }
    public int IntValue2 { get; init; }

    public PassiveRequiredPartyRole RequiredPartyRole { get; init; } = PassiveRequiredPartyRole.Any;

    /// <summary>0 = no corruption gate. Otherwise the battle <c>CorruptionTier</c> must be &gt;= this value.</summary>
    public int CorruptionMinTier { get; init; }

    /// <summary>Absolute chance to fire when conditions match. Default 1 = always.</summary>
    public double ChanceToTrigger { get; init; } = 1.0;

    /// <summary>0 = unlimited for the battle.</summary>
    public int MaxTriggersPerBattle { get; init; }

    /// <summary>0 = unlimited for the turn.</summary>
    public int MaxTriggersPerTurn { get; init; }

    public IReadOnlyList<PassiveConditionDefinition> Conditions { get; init; } = [];

    public IReadOnlyList<PassiveEffectDefinition> Effects { get; init; } = [];

    public bool HasDataDrivenEffects => Effects is { Count: > 0 };
}
