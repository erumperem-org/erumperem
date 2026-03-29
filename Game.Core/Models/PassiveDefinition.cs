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
    public int MinCasterRank { get; init; }
    public int MaxCasterRank { get; init; }
}
