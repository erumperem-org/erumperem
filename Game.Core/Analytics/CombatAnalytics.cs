using System.Globalization;
using System.Text;
using Game.Core.Domain;

namespace Game.Core.Analytics;

public sealed class CombatEvent
{
    public required string EventId { get; init; }
    public required string BattleId { get; init; }
    public required int Turn { get; init; }
    public required DateTime TimestampUtc { get; init; }
    public required BattleEventType EventType { get; init; }
    public string ActorId { get; init; } = string.Empty;
    public string TargetId { get; init; } = string.Empty;
    public string SkillId { get; init; } = string.Empty;
    public ElementType Element { get; init; }
    public bool IsHit { get; init; }
    public bool IsCrit { get; init; }
    public int DamageAmount { get; init; }
    public string DotType { get; init; } = string.Empty;
    public int DotAmount { get; init; }
    public string TokenType { get; init; } = string.Empty;
    public int TokenDelta { get; init; }
    public double CorruptionValue { get; init; }
    public int CorruptionTier { get; init; }

    /// <summary>Change applied in this event; 0 when not applicable.</summary>
    public double CorruptionDelta { get; init; }

    /// <summary>Tier before <see cref="CorruptionDelta"/> was applied; set only for <see cref="BattleEventType.CorruptionAdjusted"/>.</summary>
    public int? PreviousCorruptionTier { get; init; }
    /// <summary>Ids de passivas ativas no grupo (aliados), separados por vírgula; preenchido em <see cref="BattleEventType.BattleStarted"/>.</summary>
    public string PassiveLoadoutCsv { get; init; } = string.Empty;
    public string BattleResult { get; init; } = string.Empty;
}

public sealed class CombatEventCollector
{
    public List<CombatEvent> Events { get; } = [];

    public void Add(CombatEvent combatEvent) => Events.Add(combatEvent);
}

public sealed class CombatAggregateRow
{
    public required string EntityType { get; init; }
    public required string EntityId { get; init; }
    public required int Matches { get; init; }
    public required int Wins { get; init; }
    public required double WinRate { get; init; }
    public required int Uses { get; init; }
    public required double PickRate { get; init; }
    public required double AvgDamagePerUse { get; init; }
    public required double HitRate { get; init; }
    public required double CritRate { get; init; }
    public required double AvgDamageAtTier0 { get; init; }
    public required double AvgDamageAtTier1 { get; init; }
    public required double AvgDamageAtTier2 { get; init; }
    public required double AvgDamageAtTier3 { get; init; }
}

/// <summary>Win rate por nó passivo (batalhas em que a passiva estava desbloqueada em pelo menos um aliado).</summary>
public sealed class PassiveAggregateRow
{
    public required string PassiveId { get; init; }
    public required int BattlesWithPassive { get; init; }
    public required int Wins { get; init; }
    public required double WinRate { get; init; }
    public required double PresenceRate { get; init; }
}

public static class CombatAnalyticsExporter
{
    public static string BuildEventsCsv(IEnumerable<CombatEvent> events)
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("event_id,battle_id,turn,timestamp_utc,event_type,actor_id,target_id,skill_id,element,is_hit,is_crit,damage_amount,dot_type,dot_amount,token_type,token_delta,corruption_value,corruption_tier,corruption_delta,previous_corruption_tier,passive_loadout,battle_result");
        foreach (var combatEvent in events)
        {
            csvBuilder.AppendLine(string.Join(",",
                Esc(combatEvent.EventId),
                Esc(combatEvent.BattleId),
                combatEvent.Turn.ToString(CultureInfo.InvariantCulture),
                Esc(combatEvent.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
                Esc(combatEvent.EventType.ToString()),
                Esc(combatEvent.ActorId),
                Esc(combatEvent.TargetId),
                Esc(combatEvent.SkillId),
                Esc(combatEvent.Element.ToString()),
                Esc(combatEvent.IsHit.ToString()),
                Esc(combatEvent.IsCrit.ToString()),
                combatEvent.DamageAmount.ToString(CultureInfo.InvariantCulture),
                Esc(combatEvent.DotType),
                combatEvent.DotAmount.ToString(CultureInfo.InvariantCulture),
                Esc(combatEvent.TokenType),
                combatEvent.TokenDelta.ToString(CultureInfo.InvariantCulture),
                combatEvent.CorruptionValue.ToString(CultureInfo.InvariantCulture),
                combatEvent.CorruptionTier.ToString(CultureInfo.InvariantCulture),
                combatEvent.CorruptionDelta.ToString(CultureInfo.InvariantCulture),
                combatEvent.PreviousCorruptionTier.HasValue
                    ? combatEvent.PreviousCorruptionTier.Value.ToString(CultureInfo.InvariantCulture)
                    : string.Empty,
                Esc(combatEvent.PassiveLoadoutCsv),
                Esc(combatEvent.BattleResult)));
        }

        return csvBuilder.ToString();
    }

    public static string BuildAggregatesCsv(IEnumerable<CombatEvent> events) =>
        BuildAggregatesCsv(BuildAggregates(events));

    public static string BuildAggregatesCsv(IReadOnlyList<CombatAggregateRow> rows)
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("entity_type,entity_id,matches,wins,win_rate,uses,pick_rate,avg_damage_per_use,hit_rate,crit_rate,avg_damage_at_tier0,avg_damage_at_tier1,avg_damage_at_tier2,avg_damage_at_tier3");
        foreach (var row in rows)
        {
            csvBuilder.AppendLine(string.Join(",",
                Esc(row.EntityType),
                Esc(row.EntityId),
                row.Matches.ToString(CultureInfo.InvariantCulture),
                row.Wins.ToString(CultureInfo.InvariantCulture),
                row.WinRate.ToString(CultureInfo.InvariantCulture),
                row.Uses.ToString(CultureInfo.InvariantCulture),
                row.PickRate.ToString(CultureInfo.InvariantCulture),
                row.AvgDamagePerUse.ToString(CultureInfo.InvariantCulture),
                row.HitRate.ToString(CultureInfo.InvariantCulture),
                row.CritRate.ToString(CultureInfo.InvariantCulture),
                row.AvgDamageAtTier0.ToString(CultureInfo.InvariantCulture),
                row.AvgDamageAtTier1.ToString(CultureInfo.InvariantCulture),
                row.AvgDamageAtTier2.ToString(CultureInfo.InvariantCulture),
                row.AvgDamageAtTier3.ToString(CultureInfo.InvariantCulture)));
        }

        return csvBuilder.ToString();
    }

    public static IReadOnlyList<CombatAggregateRow> BuildAggregates(IEnumerable<CombatEvent> events)
    {
        var eventList = events.ToList();
        var battleResults = eventList
            .Where(combatEvent => combatEvent.EventType == BattleEventType.BattleEnded)
            .GroupBy(combatEvent => combatEvent.BattleId)
            .ToDictionary(endedByBattle => endedByBattle.Key, endedByBattle => endedByBattle.Last().BattleResult);

        var actionEvents = eventList.Where(combatEvent => combatEvent.EventType == BattleEventType.ActionUsed).ToList();
        var hitResolvedEvents = eventList.Where(combatEvent => combatEvent.EventType == BattleEventType.HitResolved).ToList();
        var damageEvents = eventList.Where(combatEvent => combatEvent.EventType == BattleEventType.DamageApplied).ToList();

        var alliesVictory = Side.Allies.ToString();
        var skillRows = actionEvents
            .Where(skillUse => !string.IsNullOrWhiteSpace(skillUse.SkillId))
            .GroupBy(skillUse => skillUse.SkillId)
            .Select(skillGroup =>
            {
                var groupedDamage = damageEvents.Where(damageEvent => damageEvent.SkillId == skillGroup.Key).ToList();
                var groupedHitResolution = hitResolvedEvents.Where(hitEvent => hitEvent.SkillId == skillGroup.Key).ToList();
                var tierGroups = groupedDamage
                    .GroupBy(damageEvent => damageEvent.CorruptionTier)
                    .ToDictionary(tierGroup => tierGroup.Key, tierGroup => tierGroup.ToList());
                var uses = skillGroup.Count();
                var hits = groupedHitResolution.Count(hitEvent => hitEvent.IsHit);
                var crits = groupedHitResolution.Count(hitEvent => hitEvent.IsCrit);
                var distinctBattleIds = skillGroup.Select(skillUse => skillUse.BattleId).Distinct().ToList();
                var matches = distinctBattleIds.Count;
                var wins = distinctBattleIds.Count(battleId =>
                    battleResults.TryGetValue(battleId, out var result) && result == alliesVictory);
                return new CombatAggregateRow
                {
                    EntityType = "skill",
                    EntityId = skillGroup.Key,
                    Matches = matches,
                    Wins = wins,
                    WinRate = SafeDiv(wins, matches),
                    Uses = uses,
                    PickRate = SafeDiv(uses, actionEvents.Count),
                    AvgDamagePerUse = SafeDiv(groupedDamage.Sum(damageEvent => damageEvent.DamageAmount), uses),
                    HitRate = SafeDiv(hits, uses),
                    CritRate = SafeDiv(crits, uses),
                    AvgDamageAtTier0 = SafeTierAverage(tierGroups, 0),
                    AvgDamageAtTier1 = SafeTierAverage(tierGroups, 1),
                    AvgDamageAtTier2 = SafeTierAverage(tierGroups, 2),
                    AvgDamageAtTier3 = SafeTierAverage(tierGroups, 3),
                };
            });

        return skillRows.ToList();
    }

    /// <summary>
    /// Win rate por passiva: cada batalha em que o id aparece no <see cref="CombatEvent.PassiveLoadoutCsv"/> do <see cref="BattleEventType.BattleStarted"/> conta como match;
    /// vitória = <see cref="BattleEventType.BattleEnded"/> com resultado Allies.
    /// </summary>
    public static IReadOnlyList<PassiveAggregateRow> BuildPassiveAggregates(IEnumerable<CombatEvent> events) =>
        BuildPassiveAggregates(events, allPassiveIdsInCatalog: null);

    /// <param name="allPassiveIdsInCatalog">Se não for null, inclui todas estas ids (ex.: catálogo <c>passives.json</c>), com zeros quando a passiva não apareceu em nenhuma batalha.</param>
    public static IReadOnlyList<PassiveAggregateRow> BuildPassiveAggregates(
        IEnumerable<CombatEvent> events,
        IReadOnlyCollection<string>? allPassiveIdsInCatalog)
    {
        var eventList = events.ToList();
        var battleEnds = eventList
            .Where(combatEvent => combatEvent.EventType == BattleEventType.BattleEnded)
            .GroupBy(combatEvent => combatEvent.BattleId)
            .ToDictionary(endedByBattle => endedByBattle.Key, endedByBattle => endedByBattle.Last().BattleResult);

        var battleStarts = eventList
            .Where(combatEvent => combatEvent.EventType == BattleEventType.BattleStarted)
            .GroupBy(combatEvent => combatEvent.BattleId)
            .ToDictionary(startedByBattle => startedByBattle.Key, startedByBattle => startedByBattle.First());

        var alliesVictory = Side.Allies.ToString();
        var totalBattles = battleEnds.Count;
        if (totalBattles == 0)
        {
            return [];
        }

        var perPassiveMatches = new Dictionary<string, int>(StringComparer.Ordinal);
        var perPassiveWins = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var (battleId, result) in battleEnds)
        {
            var win = string.Equals(result, alliesVictory, StringComparison.Ordinal);
            if (!battleStarts.TryGetValue(battleId, out var started))
            {
                continue;
            }

            var passiveIds = SplitPassiveCsv(started.PassiveLoadoutCsv);
            foreach (var passiveNodeId in passiveIds)
            {
                perPassiveMatches.TryGetValue(passiveNodeId, out var previousBattleCount);
                perPassiveMatches[passiveNodeId] = previousBattleCount + 1;
                if (win)
                {
                    perPassiveWins.TryGetValue(passiveNodeId, out var previousWinCount);
                    perPassiveWins[passiveNodeId] = previousWinCount + 1;
                }
            }
        }

        IEnumerable<string> rowIds = perPassiveMatches.Keys;
        if (allPassiveIdsInCatalog is { Count: > 0 })
        {
            var union = new SortedSet<string>(perPassiveMatches.Keys, StringComparer.Ordinal);
            foreach (var catalogPassiveId in allPassiveIdsInCatalog)
            {
                if (!string.IsNullOrWhiteSpace(catalogPassiveId))
                {
                    union.Add(catalogPassiveId);
                }
            }

            rowIds = union;
        }

        var rows = new List<PassiveAggregateRow>();
        foreach (var passiveNodeId in rowIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            perPassiveMatches.TryGetValue(passiveNodeId, out var matches);
            perPassiveWins.TryGetValue(passiveNodeId, out var wins);
            rows.Add(new PassiveAggregateRow
            {
                PassiveId = passiveNodeId,
                BattlesWithPassive = matches,
                Wins = wins,
                WinRate = SafeDiv(wins, matches),
                PresenceRate = SafeDiv(matches, totalBattles),
            });
        }

        return rows;
    }

    public static string BuildPassiveAggregatesCsv(IEnumerable<CombatEvent> events) =>
        BuildPassiveAggregatesCsv(BuildPassiveAggregates(events));

    public static string BuildPassiveAggregatesCsv(IReadOnlyList<PassiveAggregateRow> rows)
    {
        var csvBuilder = new StringBuilder();
        csvBuilder.AppendLine("passive_id,battles_with_passive,wins,win_rate,presence_rate");
        foreach (var row in rows)
        {
            csvBuilder.AppendLine(string.Join(",",
                Esc(row.PassiveId),
                row.BattlesWithPassive.ToString(CultureInfo.InvariantCulture),
                row.Wins.ToString(CultureInfo.InvariantCulture),
                row.WinRate.ToString(CultureInfo.InvariantCulture),
                row.PresenceRate.ToString(CultureInfo.InvariantCulture)));
        }

        return csvBuilder.ToString();
    }

    private static IEnumerable<string> SplitPassiveCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        return csv.Split(',')
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length > 0);
    }

    private static string Esc(string value)
    {
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static double SafeDiv(double numerator, double denominator)
    {
        if (Math.Abs(denominator) < double.Epsilon) return 0.0;
        return numerator / denominator;
    }

    private static double SafeTierAverage(Dictionary<int, List<CombatEvent>> tierGroups, int tier)
    {
        if (!tierGroups.TryGetValue(tier, out var values) || values.Count == 0)
        {
            return 0;
        }

        return values.Average(damageEvent => damageEvent.DamageAmount);
    }
}
