using System.Text.Json.Serialization;
using Game.Core.Config;
using Game.Core.Data;
using Game.Core.Domain;

namespace Game.Core.Models;

public sealed class DamageRange
{
    public required int Min { get; init; }
    public required int Max { get; init; }
}

public sealed class EffectSpec
{
    public required EffectType Type { get; init; }
    public TokenType? Token { get; init; }
    public DotType? Dot { get; init; }
    public int Stacks { get; init; }
    public int Potency { get; init; }
    public int Duration { get; init; }
    public int Steps { get; init; }
    public double Chance { get; init; } = 1.0;

    /// <summary>
    /// Who receives this effect relative to the hit.
    /// Default = the primary hit target; Self = caster; AllAllies / AllEnemies = living combatants on that side.
    /// </summary>
    public EffectScope EffectScope { get; init; } = EffectScope.Default;
}

public sealed class SkillDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ElementType Element { get; init; }
    public required string Type { get; init; }
    public required DamageRange BaseDamage { get; init; }
    public required double BaseCritChance { get; init; }
    public required double Accuracy { get; init; }
    public SkillTargetKind TargetKind { get; init; } = SkillTargetKind.OneEnemy;
    public IReadOnlyList<EffectSpec> EffectsOnHit { get; init; } = [];

    /// <summary>
    /// Probabilidade absoluta (0..1) de a IA considerar esta skill quando ela é elegível. Default 1.0 (sempre considerada).
    /// Skills "especiais" devem usar valores baixos (ex.: 0.20 para um golpe raro).
    /// Se nenhuma skill passar no roll, o pool elegível inteiro é usado como fallback (a IA nunca fica sem opção).
    /// </summary>
    public double ChanceToUse { get; init; } = 1.0;

    /// <summary>
    /// Trava de HP do próprio actor: a skill só fica elegível quando <c>CurrentHp / MaxHp &lt; SelfHpPercentBelow</c>.
    /// Default 1.0 (sem trava). Use 0.15 para um especial liberado abaixo de 15% de HP.
    /// </summary>
    public double SelfHpPercentBelow { get; init; } = 1.0;

    /// <summary>
    /// When a <see cref="Faction.Player"/> uses this skill, applied to world corruption: positive increases, <c>0</c> no change, negative reduces.
    /// Omitted in JSON defaults to <see cref="CorruptionRules.DefaultSkillCorruptionCost"/>.
    /// </summary>
    public double CorruptionCost { get; init; } = CorruptionRules.DefaultSkillCorruptionCost;
}

public sealed class EnemyDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required int Size { get; init; }
    public required StatsComponent BaseStats { get; init; }
    public required HealthComponent BaseHealth { get; init; }
    public required ResistanceComponent Resistances { get; init; }
    public required IReadOnlyList<string> Skills { get; init; }
    public required string AiPolicy { get; init; }
    public ElementType Element { get; init; }
}

public sealed class SkillTreeNodeDefinition
{
    public required string Id { get; init; }
    public required string Type { get; init; }

    [JsonConverter(typeof(SkillTreeNodeCostJsonConverter))]
    public required int Cost { get; init; }
    public IReadOnlyList<string> Requires { get; init; } = [];
}

public sealed class SkillTreeTierDefinition
{
    public required int Tier { get; init; }
    public IReadOnlyList<SkillTreeNodeDefinition> Nodes { get; init; } = [];
}

public sealed class SkillTreeDefinition
{
    public required ElementType Element { get; init; }
    public IReadOnlyList<SkillTreeTierDefinition> Tiers { get; init; } = [];
}

public sealed class CharacterSkillTreesDefinition
{
    public required string CharacterId { get; init; }
    public IReadOnlyList<SkillTreeDefinition> Trees { get; init; } = [];
}
