using Game.Core.Abstractions;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Engine;

public static class InitiativeResolver
{
    public const int AllyRollMinInclusive = 1;
    public const int AllyRollMaxInclusive = 10;
    public const int EnemyRollMinInclusive = 1;
    public const int EnemyRollMaxInclusive = 5;

    public static BattleInitiativeSnapshot RollInitiative(BattleState state, IRandomSource random)
    {
        var rollsByCombatantId = new Dictionary<string, int>(StringComparer.Ordinal);
        var allyTeamTotal = 0;

        foreach (var ally in state.Allies)
        {
            if (!BattleState.IsActiveBattler(ally))
            {
                continue;
            }

            var allyRoll = random.Next(AllyRollMinInclusive, AllyRollMaxInclusive + 1);
            rollsByCombatantId[ally.Identity.Id] = allyRoll;
            allyTeamTotal += allyRoll;
        }

        var enemyTeamTotal = 0;
        foreach (var enemy in state.Enemies)
        {
            var enemyRoll = random.Next(EnemyRollMinInclusive, EnemyRollMaxInclusive + 1);
            rollsByCombatantId[enemy.Identity.Id] = enemyRoll;
            enemyTeamTotal += enemyRoll;
        }

        var firstActingSide = ResolveFirstActingSide(allyTeamTotal, enemyTeamTotal, random);

        return new BattleInitiativeSnapshot
        {
            FirstActingSide = firstActingSide,
            AllyTeamTotal = allyTeamTotal,
            EnemyTeamTotal = enemyTeamTotal,
            RollsByCombatantId = rollsByCombatantId,
        };
    }

    /// <summary>
    /// Ordem fixa por ronda: main → companion (aliados) e rank 1 → 4 (inimigos);
    /// a equipa vencedora da iniciativa age primeiro em cada ciclo.
    /// </summary>
    public static List<Combatant> BuildTurnOrder(BattleState state)
    {
        if (state.Initiative is null)
        {
            throw new InvalidOperationException("Initiative must be resolved before building turn order.");
        }

        var orderedAllies = state.Allies
            .Where(BattleState.IsActiveBattler)
            .ToList();

        var orderedEnemies = state.Enemies
            .Where(BattleState.IsActiveBattler)
            .OrderBy(enemy => enemy.Position.FrontRank)
            .ToList();

        var turnOrder = new List<Combatant>(orderedAllies.Count + orderedEnemies.Count);
        if (state.Initiative.FirstActingSide == Side.Allies)
        {
            turnOrder.AddRange(orderedAllies);
            turnOrder.AddRange(orderedEnemies);
        }
        else
        {
            turnOrder.AddRange(orderedEnemies);
            turnOrder.AddRange(orderedAllies);
        }

        return turnOrder;
    }

    private static Side ResolveFirstActingSide(int allyTeamTotal, int enemyTeamTotal, IRandomSource random)
    {
        if (allyTeamTotal > enemyTeamTotal)
        {
            return Side.Allies;
        }

        if (enemyTeamTotal > allyTeamTotal)
        {
            return Side.Enemies;
        }

        return random.Next(0, 2) == 0 ? Side.Allies : Side.Enemies;
    }
}
