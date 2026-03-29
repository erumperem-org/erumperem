using Game.Core.Config;
using Game.Core.Domain;

namespace Game.Core.Models;

public sealed class BattleState
{
    public required IList<Combatant> Allies { get; init; }
    public required IList<Combatant> Enemies { get; init; }
    public required IDictionary<string, SkillDefinition> SkillsById { get; init; }

    /// <summary>Catálogo de passivas (efeitos); chave = id do nó passivo.</summary>
    public IReadOnlyDictionary<string, PassiveDefinition> PassivesById { get; init; } =
        new Dictionary<string, PassiveDefinition>();

    public required CombatBalanceConfig BalanceConfig { get; init; }
    public required double CorruptionValue { get; set; }
    public required int TurnNumber { get; set; }
    public required Guid BattleId { get; init; }

    public IEnumerable<Combatant> GetAllCombatants()
    {
        return Allies.Concat(Enemies);
    }

    public int CorruptionTier => CorruptionTierCalculator.GetTier(CorruptionValue);

    public bool IsFinished =>
        Allies.All(c => c.Health.IsDead) || Enemies.All(c => c.Health.IsDead);

    public Side? Winner
    {
        get
        {
            if (Allies.All(c => c.Health.IsDead)) return Side.Enemies;
            if (Enemies.All(c => c.Health.IsDead)) return Side.Allies;
            return null;
        }
    }

    /// <summary>Ids de passivas desbloqueadas em pelo menos um aliado (intersecção com <see cref="PassivesById"/>), ordenados para telemetria.</summary>
    public string GetPassiveLoadoutCsv()
    {
        var ids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var passiveId in PassivesById.Keys)
        {
            foreach (var ally in Allies)
            {
                if (ally.Progression.UnlockedNodes.TryGetValue(passiveId, out var on) && on)
                {
                    ids.Add(passiveId);
                }
            }
        }

        return string.Join(",", ids);
    }
}

public sealed class ChosenAction
{
    public required Combatant Actor { get; init; }
    public required Combatant Target { get; init; }
    public required SkillDefinition Skill { get; init; }
    public required ActionType ActionType { get; init; }
}

public sealed class ResolveActionResult
{
    public required bool IsHit { get; init; }
    public required bool IsCrit { get; init; }
    public required int DamageApplied { get; init; }
}
