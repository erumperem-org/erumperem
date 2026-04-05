using Game.Core.Domain;

namespace Game.Core.Models;

public sealed class IdentityComponent
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required Faction Faction { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
}

public sealed class HealthComponent
{
    public required int CurrentHp { get; set; }
    public required int MaxHp { get; init; }
    public bool IsDead { get; set; }
    public bool IsDeathblowPending { get; set; }
}

public sealed class PositionComponent
{
    public required Side Side { get; init; }
    public required int FrontRank { get; set; }
    public required int Size { get; init; }

    public IReadOnlyList<int> OccupiedRanks =>
        Enumerable.Range(FrontRank, Size).ToArray();
}

public sealed class StatsComponent
{
    public required int Speed { get; init; }
    public required double Accuracy { get; init; }
    public required double CritChance { get; init; }
}

public sealed class ResistanceComponent
{
    public required double BurnRes { get; init; }
    public required double BlightRes { get; init; }
    public required double MoveRes { get; init; }
    public required double StunRes { get; init; }
    public required double DeathblowRes { get; init; }
}

public sealed class TokenEntry
{
    public required TokenType Type { get; init; }
    public int Stacks { get; set; }
}

public sealed class TokenComponent
{
    public List<TokenEntry> Entries { get; } = [];

    public int GetStacks(TokenType tokenType)
    {
        return Entries.FirstOrDefault(tokenEntry => tokenEntry.Type == tokenType)?.Stacks ?? 0;
    }

    public void Add(TokenType tokenType, int stacks)
    {
        var entry = Entries.FirstOrDefault(tokenEntry => tokenEntry.Type == tokenType);
        if (entry is null)
        {
            Entries.Add(new TokenEntry { Type = tokenType, Stacks = stacks });
            return;
        }

        entry.Stacks += stacks;
    }

    public bool ConsumeOne(TokenType tokenType)
    {
        var entry = Entries.FirstOrDefault(tokenEntry => tokenEntry.Type == tokenType);
        if (entry is null || entry.Stacks <= 0)
        {
            return false;
        }

        entry.Stacks--;
        return true;
    }
}

public sealed class DotInstance
{
    public required DotType Type { get; init; }
    public required int Potency { get; init; }
    public required int RemainingTurns { get; set; }
    public required string AppliedById { get; init; }
}

public sealed class DotComponent
{
    public List<DotInstance> ActiveDots { get; } = [];
}

public sealed class SkillLoadoutComponent
{
    public List<string> Skills { get; } = [];
    public Dictionary<string, int> Cooldowns { get; } = [];
}

public sealed class ProgressionComponent
{
    public required int Level { get; set; }
    public required int SpentPoints { get; set; }
    public Dictionary<string, bool> UnlockedNodes { get; } = [];
}

public sealed class AIComponent
{
    public required string DecisionPolicyId { get; init; }
}

public sealed class ElementAffinityComponent
{
    public required ElementType Element { get; init; }
}

public sealed class Combatant
{
    public required IdentityComponent Identity { get; set; }
    public required HealthComponent Health { get; set; }
    public required PositionComponent Position { get; set; }
    public required StatsComponent Stats { get; set; }
    public required ResistanceComponent Resistances { get; set; }
    public required TokenComponent Tokens { get; set; }
    public required DotComponent Dots { get; set; }
    public required SkillLoadoutComponent SkillLoadout { get; set; }
    public required ProgressionComponent Progression { get; set; }
    public PassiveRuntimeState PassiveRuntime { get; set; } = new();
    public AIComponent? AI { get; set; }
    public required ElementAffinityComponent ElementAffinity { get; set; }
}
