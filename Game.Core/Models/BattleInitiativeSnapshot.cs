using Game.Core.Domain;

namespace Game.Core.Models;

/// <summary>
/// Resultado da rolagem de iniciativa no início do combate (uma vez por batalha).
/// </summary>
public sealed class BattleInitiativeSnapshot
{
    public required Side FirstActingSide { get; init; }
    public required int AllyTeamTotal { get; init; }
    public required int EnemyTeamTotal { get; init; }
    public required IReadOnlyDictionary<string, int> RollsByCombatantId { get; init; }
}
