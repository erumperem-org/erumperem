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
        var sb = new StringBuilder();
        sb.AppendLine("event_id,battle_id,turn,timestamp_utc,event_type,actor_id,target_id,skill_id,element,is_hit,is_crit,damage_amount,dot_type,dot_amount,token_type,token_delta,corruption_value,corruption_tier,passive_loadout,battle_result");
        foreach (var e in events)
        {
            sb.AppendLine(string.Join(",",
                Esc(e.EventId),
                Esc(e.BattleId),
                e.Turn.ToString(CultureInfo.InvariantCulture),
                Esc(e.TimestampUtc.ToString("O", CultureInfo.InvariantCulture)),
                Esc(e.EventType.ToString()),
                Esc(e.ActorId),
                Esc(e.TargetId),
                Esc(e.SkillId),
                Esc(e.Element.ToString()),
                Esc(e.IsHit.ToString()),
                Esc(e.IsCrit.ToString()),
                e.DamageAmount.ToString(CultureInfo.InvariantCulture),
                Esc(e.DotType),
                e.DotAmount.ToString(CultureInfo.InvariantCulture),
                Esc(e.TokenType),
                e.TokenDelta.ToString(CultureInfo.InvariantCulture),
                e.CorruptionValue.ToString(CultureInfo.InvariantCulture),
                e.CorruptionTier.ToString(CultureInfo.InvariantCulture),
                Esc(e.PassiveLoadoutCsv),
                Esc(e.BattleResult)));
        }

        return sb.ToString();
    }

    public static string BuildAggregatesCsv(IEnumerable<CombatEvent> events) =>
        BuildAggregatesCsv(BuildAggregates(events));

    public static string BuildAggregatesCsv(IReadOnlyList<CombatAggregateRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("entity_type,entity_id,matches,wins,win_rate,uses,pick_rate,avg_damage_per_use,hit_rate,crit_rate,avg_damage_at_tier0,avg_damage_at_tier1,avg_damage_at_tier2,avg_damage_at_tier3");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
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

        return sb.ToString();
    }

    public static IReadOnlyList<CombatAggregateRow> BuildAggregates(IEnumerable<CombatEvent> events)
    {
        var eventList = events.ToList();
        var battleResults = eventList
            .Where(e => e.EventType == BattleEventType.BattleEnded)
            .GroupBy(e => e.BattleId)
            .ToDictionary(g => g.Key, g => g.Last().BattleResult);

        var actionEvents = eventList.Where(e => e.EventType == BattleEventType.ActionUsed).ToList();
        var hitResolvedEvents = eventList.Where(e => e.EventType == BattleEventType.HitResolved).ToList();
        var damageEvents = eventList.Where(e => e.EventType == BattleEventType.DamageApplied).ToList();

        var alliesVictory = Side.Allies.ToString();
        var skillRows = actionEvents
            .Where(e => !string.IsNullOrWhiteSpace(e.SkillId))
            .GroupBy(e => e.SkillId)
            .Select(g =>
            {
                var groupedDamage = damageEvents.Where(d => d.SkillId == g.Key).ToList();
                var groupedHitResolution = hitResolvedEvents.Where(h => h.SkillId == g.Key).ToList();
                var tierGroups = groupedDamage.GroupBy(d => d.CorruptionTier).ToDictionary(x => x.Key, x => x.ToList());
                var uses = g.Count();
                var hits = groupedHitResolution.Count(x => x.IsHit);
                var crits = groupedHitResolution.Count(x => x.IsCrit);
                var distinctBattleIds = g.Select(x => x.BattleId).Distinct().ToList();
                var matches = distinctBattleIds.Count;
                var wins = distinctBattleIds.Count(bid =>
                    battleResults.TryGetValue(bid, out var result) && result == alliesVictory);
                return new CombatAggregateRow
                {
                    EntityType = "skill",
                    EntityId = g.Key,
                    Matches = matches,
                    Wins = wins,
                    WinRate = SafeDiv(wins, matches),
                    Uses = uses,
                    PickRate = SafeDiv(uses, actionEvents.Count),
                    AvgDamagePerUse = SafeDiv(groupedDamage.Sum(x => x.DamageAmount), uses),
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
            .Where(e => e.EventType == BattleEventType.BattleEnded)
            .GroupBy(e => e.BattleId)
            .ToDictionary(g => g.Key, g => g.Last().BattleResult);

        var battleStarts = eventList
            .Where(e => e.EventType == BattleEventType.BattleStarted)
            .GroupBy(e => e.BattleId)
            .ToDictionary(g => g.Key, g => g.First());

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
            foreach (var pid in passiveIds)
            {
                perPassiveMatches.TryGetValue(pid, out var m);
                perPassiveMatches[pid] = m + 1;
                if (win)
                {
                    perPassiveWins.TryGetValue(pid, out var w);
                    perPassiveWins[pid] = w + 1;
                }
            }
        }

        IEnumerable<string> rowIds = perPassiveMatches.Keys;
        if (allPassiveIdsInCatalog is { Count: > 0 })
        {
            var union = new SortedSet<string>(perPassiveMatches.Keys, StringComparer.Ordinal);
            foreach (var id in allPassiveIdsInCatalog)
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    union.Add(id);
                }
            }

            rowIds = union;
        }

        var rows = new List<PassiveAggregateRow>();
        foreach (var pid in rowIds.OrderBy(x => x, StringComparer.Ordinal))
        {
            perPassiveMatches.TryGetValue(pid, out var matches);
            perPassiveWins.TryGetValue(pid, out var wins);
            rows.Add(new PassiveAggregateRow
            {
                PassiveId = pid,
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
        var sb = new StringBuilder();
        sb.AppendLine("passive_id,battles_with_passive,wins,win_rate,presence_rate");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Esc(row.PassiveId),
                row.BattlesWithPassive.ToString(CultureInfo.InvariantCulture),
                row.Wins.ToString(CultureInfo.InvariantCulture),
                row.WinRate.ToString(CultureInfo.InvariantCulture),
                row.PresenceRate.ToString(CultureInfo.InvariantCulture)));
        }

        return sb.ToString();
    }

    private static IEnumerable<string> SplitPassiveCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return [];
        }

        return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

        return values.Average(x => x.DamageAmount);
    }
}
