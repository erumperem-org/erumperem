using Game.Core.Domain;

namespace Game.Core.Items;

public sealed class CombatItemStatModifier
{
    public CombatItemStatKind Stat { get; init; }
    public CombatItemModifierScale Scale { get; init; }

    /// <summary>+1 = +, +2 = ++, +3 = +++, +4 = ++++; negatives are mirrored.</summary>
    public int IntensityRank { get; init; }
}

public sealed class CombatItemDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Description { get; init; }
    public required CombatItemRarity Rarity { get; init; }
    public required CombatItemKind Kind { get; init; }

    /// <summary>Same icon per item family; rarity only changes the background color.</summary>
    public required string IconFamilyId { get; init; }

    public IReadOnlyList<CombatItemStatModifier> StatModifiers { get; init; } = [];
    public CombatItemUtilityKind UtilityKind { get; init; } = CombatItemUtilityKind.None;
}

public sealed class CombatantBaseStatSnapshot
{
    public int MaxHp { get; init; }
    public double DefenseChance { get; init; }
    public double CritChance { get; init; }
    public int Speed { get; init; }
    public double Accuracy { get; init; }
}
