using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;
using Game.Core.Presentation;

namespace Game.Core.Almanac;

public sealed class EnemyAlmanacSkillView
{
    public required string SkillId { get; init; }
    public required string DisplayName { get; init; }
    public required string FullDescription { get; init; }
    public required bool IsRevealed { get; init; }
}

public sealed class EnemyAlmanacEntry
{
    public required string EnemyCatalogId { get; init; }
    public required string DisplayName { get; init; }
    public required ElementType Element { get; init; }
    public required IReadOnlyList<EnemyAlmanacSkillView> ActiveSkills { get; init; }
    public required IReadOnlyList<string> PassiveSummaries { get; init; }

    public const string HiddenSkillDisplayName = "Unknown";
    public const string HiddenSkillDescription = "This skill has not been seen yet.";
}

public static class EnemyAlmanacEntryBuilder
{
    public static EnemyAlmanacEntry Build(
        string enemyCatalogId,
        EnemyAlmanacProgress progress,
        EnemyDefinition? catalogDefinition,
        Combatant? liveCombatant,
        IReadOnlyDictionary<string, SkillDefinition> skillsById,
        IReadOnlyDictionary<string, PassiveDefinition>? passivesById = null)
    {
        var resolvedCatalogId = string.IsNullOrWhiteSpace(enemyCatalogId)
            ? catalogDefinition?.Id ?? string.Empty
            : enemyCatalogId;
        var displayName = catalogDefinition?.Name
                          ?? liveCombatant?.Identity.DisplayName
                          ?? resolvedCatalogId;
        var element = catalogDefinition?.Element
                      ?? liveCombatant?.ElementAffinity.Element
                      ?? ElementType.None;

        var activeSkillIds = ResolveActiveSkillIds(catalogDefinition, liveCombatant);
        var activeSkills = new List<EnemyAlmanacSkillView>();
        foreach (var skillId in activeSkillIds)
        {
            var isRevealed = progress.HasRevealedActiveSkill(resolvedCatalogId, skillId);
            if (!isRevealed)
            {
                activeSkills.Add(new EnemyAlmanacSkillView
                {
                    SkillId = skillId,
                    DisplayName = EnemyAlmanacEntry.HiddenSkillDisplayName,
                    FullDescription = EnemyAlmanacEntry.HiddenSkillDescription,
                    IsRevealed = false,
                });
                continue;
            }

            skillsById.TryGetValue(skillId, out var skillDefinition);
            var skillName = skillDefinition != null
                ? SkillPlayerDescriptionBuilder.TranslateToEnglish(skillDefinition.Name)
                : skillId;
            var fullDescription = skillDefinition != null
                ? SkillPlayerDescriptionBuilder.BuildSummaryLine(skillDefinition)
                : skillId;

            activeSkills.Add(new EnemyAlmanacSkillView
            {
                SkillId = skillId,
                DisplayName = skillName,
                FullDescription = fullDescription,
                IsRevealed = true,
            });
        }

        return new EnemyAlmanacEntry
        {
            EnemyCatalogId = resolvedCatalogId,
            DisplayName = displayName,
            Element = element,
            ActiveSkills = activeSkills,
            PassiveSummaries = ResolvePassiveSummaries(catalogDefinition, liveCombatant, passivesById),
        };
    }

    public static string FormatPlayerFacingText(EnemyAlmanacEntry entry)
    {
        var lines = new List<string>
        {
            $"{entry.DisplayName} — Element: {entry.Element}",
            "Active skills:",
        };

        if (entry.ActiveSkills.Count == 0)
        {
            lines.Add("- None");
        }
        else
        {
            foreach (var activeSkill in entry.ActiveSkills)
            {
                lines.Add(activeSkill.IsRevealed
                    ? $"- {activeSkill.FullDescription}"
                    : $"- {activeSkill.DisplayName}: {activeSkill.FullDescription}");
            }
        }

        lines.Add("Passives:");
        if (entry.PassiveSummaries.Count == 0)
        {
            lines.Add("- None");
        }
        else
        {
            foreach (var passiveSummary in entry.PassiveSummaries)
            {
                lines.Add($"- {passiveSummary}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static List<string> ResolveActiveSkillIds(EnemyDefinition? catalogDefinition, Combatant? liveCombatant)
    {
        var skillIds = new List<string>();
        var seenSkillIds = new HashSet<string>(StringComparer.Ordinal);

        void AddSkillId(string? skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId) || !seenSkillIds.Add(skillId))
            {
                return;
            }

            skillIds.Add(skillId);
        }

        if (catalogDefinition?.Skills != null)
        {
            foreach (var skillId in catalogDefinition.Skills)
            {
                AddSkillId(skillId);
            }
        }

        if (liveCombatant?.SkillLoadout?.Skills != null)
        {
            foreach (var skillId in liveCombatant.SkillLoadout.Skills)
            {
                AddSkillId(skillId);
            }
        }

        return skillIds;
    }

    private static List<string> ResolvePassiveSummaries(
        EnemyDefinition? catalogDefinition,
        Combatant? liveCombatant,
        IReadOnlyDictionary<string, PassiveDefinition>? passivesById)
    {
        var summaries = new List<string>();
        var seenPassiveIds = new HashSet<string>(StringComparer.Ordinal);

        void AddPassiveId(string? passiveId)
        {
            if (string.IsNullOrWhiteSpace(passiveId) || !seenPassiveIds.Add(passiveId))
            {
                return;
            }

            if (passivesById != null && passivesById.TryGetValue(passiveId, out var passiveDefinition))
            {
                summaries.Add(FormatPassiveSummary(passiveDefinition));
                return;
            }

            summaries.Add(passiveId);
        }

        if (catalogDefinition?.PassiveIds != null)
        {
            foreach (var passiveId in catalogDefinition.PassiveIds)
            {
                AddPassiveId(passiveId);
            }
        }

        if (liveCombatant?.Progression?.UnlockedNodes == null)
        {
            return summaries;
        }

        foreach (var unlockedNode in liveCombatant.Progression.UnlockedNodes)
        {
            if (!unlockedNode.Value)
            {
                continue;
            }

            if (passivesById != null && !passivesById.ContainsKey(unlockedNode.Key))
            {
                continue;
            }

            AddPassiveId(unlockedNode.Key);
        }

        return summaries;
    }

    private static string FormatPassiveSummary(PassiveDefinition passiveDefinition)
    {
        if (passiveDefinition.HasDataDrivenEffects && passiveDefinition.Effects.Count > 0)
        {
            var firstEffect = passiveDefinition.Effects[0];
            return $"{passiveDefinition.Id} ({firstEffect.Operation})";
        }

        return $"{passiveDefinition.Id} ({passiveDefinition.EffectKind})";
    }
}
