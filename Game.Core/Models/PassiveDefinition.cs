using System.Text.Json.Serialization;
using Game.Core.Domain;
using Game.Core.Passives;

namespace Game.Core.Models;

/// <summary>
/// Definição carregada de JSON; o <see cref="Id"/> deve coincidir com um nó <c>Passive</c> em <c>skill_trees.json</c>.
/// </summary>
public sealed class PassiveDefinition
{
    public required string Id { get; init; }

    /// <summary>
    /// Legacy engine path. Only Horse Boss summon still uses this in the live catalog.
    /// Default (0 / DamageCausedVsSkillId) is omitted on write so hero passives stay Conditions + Effects.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public PassiveEffectKind EffectKind { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? SkillId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string? PrerequisiteSkillId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public DotType? DotType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TokenType? TokenType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TokenType? GrantTokenType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TokenType? IfHasTokenType { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public TokenType? UnlessHasTokenType { get; init; }

    /// <summary>Bónus aditivo ao multiplicador de dano (ex.: 0.10 = +10%).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Additive { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double AdditivePerStack { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double Cap { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public double HpBelowPercent { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public int IntValue { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
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
