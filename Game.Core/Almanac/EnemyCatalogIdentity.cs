using Game.Core.Models;

namespace Game.Core.Almanac;

public static class EnemyCatalogIdentity
{
    public const string ArchetypeTagPrefix = "Archetype:";

    public static string FormatArchetypeTag(string enemyCatalogId) =>
        $"{ArchetypeTagPrefix}{enemyCatalogId}";

    public static bool TryResolveEnemyCatalogId(Combatant combatant, out string enemyCatalogId)
    {
        enemyCatalogId = string.Empty;
        if (combatant?.Identity?.Tags == null)
        {
            return false;
        }

        foreach (var tag in combatant.Identity.Tags)
        {
            if (string.IsNullOrWhiteSpace(tag) ||
                !tag.StartsWith(ArchetypeTagPrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var resolvedCatalogId = tag.Substring(ArchetypeTagPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(resolvedCatalogId))
            {
                continue;
            }

            enemyCatalogId = resolvedCatalogId;
            return true;
        }

        return false;
    }

    public static void AssignArchetypeTag(Combatant combatant, string enemyCatalogId)
    {
        if (combatant?.Identity == null || string.IsNullOrWhiteSpace(enemyCatalogId))
        {
            return;
        }

        var tags = combatant.Identity.Tags?.ToList() ?? [];
        tags.RemoveAll(tag =>
            !string.IsNullOrEmpty(tag) &&
            tag.StartsWith(ArchetypeTagPrefix, StringComparison.OrdinalIgnoreCase));
        tags.Add(FormatArchetypeTag(enemyCatalogId));

        var existingIdentity = combatant.Identity;
        combatant.Identity = new IdentityComponent
        {
            Id = existingIdentity.Id,
            DisplayName = existingIdentity.DisplayName,
            Faction = existingIdentity.Faction,
            Tags = tags,
        };
    }

    public static string NormalizeCatalogId(
        string visualOrCatalogId,
        IReadOnlyDictionary<string, EnemyDefinition>? enemyDefinitionsById = null)
    {
        if (string.IsNullOrWhiteSpace(visualOrCatalogId))
        {
            return string.Empty;
        }

        var trimmedId = visualOrCatalogId.Trim();
        if (enemyDefinitionsById != null &&
            enemyDefinitionsById.TryGetValue(trimmedId, out var matchedDefinition))
        {
            return matchedDefinition.Id;
        }

        return trimmedId;
    }
}
