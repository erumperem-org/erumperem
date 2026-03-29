using Game.Core.Domain;

namespace Game.Core.Models;

public sealed class DamageRange
{
    public required int Min { get; init; }
    public required int Max { get; init; }
}

public sealed class MoveSpec
{
    public required string Type { get; init; }
    public required int Steps { get; init; }
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

    /// <summary>Default = single target; AllAllies = same-side party (e.g. Muralha Block on everyone).</summary>
    public string EffectScope { get; init; } = "Default";
}

public sealed class SkillDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required ElementType Element { get; init; }
    public required string Type { get; init; }
    public IReadOnlyList<int> AllowedCasterRanks { get; init; } = [];
    public IReadOnlyList<int> AllowedTargetRanks { get; init; } = [];
    public required DamageRange BaseDamage { get; init; }
    public required double BaseCritChance { get; init; }
    public required double Accuracy { get; init; }
    public int Cooldown { get; init; }
    public SkillTargetKind TargetKind { get; init; } = SkillTargetKind.Enemy;
    public MoveSpec SelfMove { get; init; } = new MoveSpec { Type = "None", Steps = 0 };
    public IReadOnlyList<EffectSpec> EffectsOnHit { get; init; } = [];
    public IReadOnlyList<EffectSpec> ComboBonus { get; init; } = [];
    public int Weight { get; init; } = 1;
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
