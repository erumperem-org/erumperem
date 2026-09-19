using Game.Core.Domain;

namespace Game.Core.Models;

/// <summary>
/// Authoring condition for a passive. Serialized in <c>passives.json</c>.
/// </summary>
public sealed class PassiveConditionDefinition
{
    public PassiveActivationKind Activation { get; init; } = PassiveActivationKind.Permanent;

    public TokenType? RequiredStatus { get; init; }

    public PassiveStatusMatchKind StatusMatch { get; init; } = PassiveStatusMatchKind.ListedOrAny;

    /// <summary>For UponDamageTaken: trigger once per this many HP lost (0 = unused).</summary>
    public int HitPointsLostPerTrigger { get; init; }

    /// <summary>For UponStatThreshold: Current / Max fraction that must be crossed (0 = unused).</summary>
    public double StatThresholdFraction { get; init; }

    public PassiveStatThresholdComparison StatThresholdComparison { get; init; } =
        PassiveStatThresholdComparison.BelowOrEqual;

    public string? SkillId { get; init; }
}

/// <summary>
/// Authoring effect for a passive. Serialized in <c>passives.json</c>.
/// Horse Boss summon uses <see cref="PassiveEffectOperationKind.SummonEnemy"/> on enemy passives only.
/// </summary>
public sealed class PassiveEffectDefinition
{
    public PassiveEffectOperationKind Operation { get; init; }

    public double Magnitude { get; init; }

    public TokenType? Token { get; init; }

    public string? SkillId { get; init; }

    public string? SummonEnemyId { get; init; }

    public int Stacks { get; init; }

    /// <summary>Absolute chance to apply this effect when the parent passive fires. Default 1 = always.</summary>
    public double ChanceToTrigger { get; init; } = 1.0;

    /// <summary>0 = unlimited for the battle.</summary>
    public int MaxTriggersPerBattle { get; init; }

    /// <summary>0 = unlimited for the turn.</summary>
    public int MaxTriggersPerTurn { get; init; }

    public PassiveCharacterStatKind CharacterStat { get; init; } = PassiveCharacterStatKind.None;

    public PassiveSkillStatKind SkillStat { get; init; } = PassiveSkillStatKind.None;

    public PassiveResourceKind Resource { get; init; } = PassiveResourceKind.None;

    public PassiveStatChangeTarget StatChangeTarget { get; init; } = PassiveStatChangeTarget.Self;

    public PassiveTokenManipulationMode TokenManipulationMode { get; init; } =
        PassiveTokenManipulationMode.Apply;

    /// <summary>
    /// Resource token when <see cref="Resource"/> reads stacks but <see cref="Token"/> is the output
    /// (e.g. Destabilization efficiency scaled by Controlled Instability).
    /// </summary>
    public TokenType? ResourceToken { get; init; }

    /// <summary>Extra stacks = event token delta * this / divisor. 0 = unused.</summary>
    public int ScaleStacksPerSourceStack { get; init; }

    /// <summary>Divisor for <see cref="ScaleStacksPerSourceStack"/>. 0 = unused.</summary>
    public int ScaleStacksSourceDivisor { get; init; }

    /// <summary>
    /// CharacterStatChange DefenseChance (and similar) lasts until the owner's next turn start
    /// (end of the opposing side's actions).
    /// </summary>
    public bool ExpiresAtEndOfOpposingSideTurn { get; init; }

    /// <summary>Absolute chance that one stack of <see cref="Token"/> is not lost at end of turn.</summary>
    public double SkipEndOfTurnDecayChance { get; init; }
}
