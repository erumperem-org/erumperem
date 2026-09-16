using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Domain;
using Game.Core.Items;
using Game.Core.Models;

namespace Game.Core.Data;

public static class CombatDataLoader
{
    /// <summary>
    /// Locates <c>Assets/StreamingAssets/Data/skills.json</c> (walk-up from the repo), then <c>AppContext/Data</c>.
    /// </summary>
    public static string ResolveDefaultSkillsPath() => ResolveDefaultDataPath("skills.json");

    /// <summary>
    /// Locates <c>Assets/StreamingAssets/Data/passives.json</c> (mesmo padrão que <see cref="ResolveDefaultSkillsPath"/>).
    /// </summary>
    public static string ResolveDefaultPassivesPath() => ResolveDefaultDataPath("passives.json");

    /// <summary>Localiza <c>Assets/StreamingAssets/Data/enemies.json</c>.</summary>
    public static string ResolveDefaultEnemiesPath() => ResolveDefaultDataPath("enemies.json");

    /// <summary>Localiza <c>Assets/StreamingAssets/Data/skill_trees.json</c>.</summary>
    public static string ResolveDefaultSkillTreesPath() => ResolveDefaultDataPath("skill_trees.json");

    /// <summary>Localiza <c>Assets/StreamingAssets/Data/items.json</c>.</summary>
    public static string ResolveDefaultItemsPath() => ResolveDefaultDataPath("items.json");

    /// <summary>
    /// Canonical runtime catalog directory: <c>Assets/StreamingAssets/Data</c> when the repo is on disk,
    /// otherwise <c>AppContext.BaseDirectory/Data</c> for a published CLI.
    /// </summary>
    public static string ResolveDefaultCatalogDirectory()
    {
        foreach (var startDirectory in EnumerateSearchStartDirectories())
        {
            var currentDirectory = new DirectoryInfo(startDirectory);
            while (currentDirectory != null)
            {
                var streamingAssetsDataDirectory = Path.Combine(
                    currentDirectory.FullName,
                    "Assets",
                    "StreamingAssets",
                    "Data");
                if (File.Exists(Path.Combine(streamingAssetsDataDirectory, "skills.json")))
                {
                    return streamingAssetsDataDirectory;
                }

                currentDirectory = currentDirectory.Parent;
            }
        }

        var publishedDataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        if (File.Exists(Path.Combine(publishedDataDirectory, "skills.json")))
        {
            return publishedDataDirectory;
        }

        throw new DirectoryNotFoundException(
            "Combat catalog directory not found. Expected Assets/StreamingAssets/Data (walk-up from the repo) " +
            "or AppContext.BaseDirectory/Data.");
    }

    public static string ResolveDefaultDataPath(string fileName)
    {
        var catalogDirectory = ResolveDefaultCatalogDirectory();
        var filePath = Path.Combine(catalogDirectory, fileName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"{fileName} not found in catalog directory {catalogDirectory}.");
        }

        return filePath;
    }

    private static IEnumerable<string> EnumerateSearchStartDirectories()
    {
        var seenDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (TryAddUniqueDirectory(seenDirectories, AppContext.BaseDirectory))
        {
            yield return AppContext.BaseDirectory;
        }

        var currentWorkingDirectory = Directory.GetCurrentDirectory();
        if (TryAddUniqueDirectory(seenDirectories, currentWorkingDirectory))
        {
            yield return currentWorkingDirectory;
        }
    }

    private static bool TryAddUniqueDirectory(HashSet<string> seenDirectories, string? directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        var fullDirectoryPath = Path.GetFullPath(directoryPath);
        return seenDirectories.Add(fullDirectoryPath);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters =
        {
            new SkillTargetKindJsonConverter(),
            new PassiveEffectKindJsonConverter(),
            new JsonStringEnumConverter(),
        },
    };

    public static IReadOnlyList<SkillDefinition> LoadSkills(string path)
    {
        var json = File.ReadAllText(path);
        RejectRemovedSkillContractProperties(json);
        var skills = JsonSerializer.Deserialize<List<SkillDefinition>>(json, JsonOptions) ?? [];
        ValidateSkills(skills);
        return skills;
    }

    public static IReadOnlyList<EnemyDefinition> LoadEnemies(string path)
    {
        var json = File.ReadAllText(path);
        var enemies = JsonSerializer.Deserialize<List<EnemyDefinition>>(json, JsonOptions) ?? [];
        ValidateEnemies(enemies);
        return enemies;
    }

    public static IReadOnlyList<CharacterSkillTreesDefinition> LoadSkillTrees(string path)
    {
        var json = File.ReadAllText(path);
        var trees = JsonSerializer.Deserialize<List<CharacterSkillTreesDefinition>>(json, JsonOptions) ?? [];
        ValidateSkillTrees(trees);
        return trees;
    }

    public static IReadOnlyList<PassiveDefinition> LoadPassives(string path)
    {
        var json = File.ReadAllText(path);
        var passives = JsonSerializer.Deserialize<List<PassiveDefinition>>(json, JsonOptions) ?? [];
        ValidatePassives(passives);
        return passives;
    }

    public static IReadOnlyList<CombatItemDefinition> LoadItems(string path)
    {
        var json = File.ReadAllText(path);
        var items = JsonSerializer.Deserialize<List<CombatItemDefinition>>(json, JsonOptions) ?? [];
        ValidateItems(items);
        return items;
    }

    public static IReadOnlyDictionary<string, EnemyDefinition> BuildEnemyDefinitionIndex(
        IEnumerable<EnemyDefinition> enemyDefinitions)
    {
        var index = new Dictionary<string, EnemyDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var enemyDefinition in enemyDefinitions)
        {
            index[enemyDefinition.Id] = enemyDefinition;
            if (string.Equals(enemyDefinition.Id, "corrupted_fairy", StringComparison.OrdinalIgnoreCase))
            {
                index["CorruptedFairy"] = enemyDefinition;
            }

            if (string.Equals(enemyDefinition.Id, "horse_boss", StringComparison.OrdinalIgnoreCase))
            {
                index["HorseBoss"] = enemyDefinition;
            }
        }

        return index;
    }

    private static void RejectRemovedSkillContractProperties(string skillsJson)
    {
        using var document = JsonDocument.Parse(skillsJson);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var skillElement in document.RootElement.EnumerateArray())
        {
            var skillId = skillElement.TryGetProperty("id", out var idElement)
                ? idElement.GetString() ?? "(unknown)"
                : "(unknown)";

            foreach (var property in skillElement.EnumerateObject())
            {
                if (string.Equals(property.Name, "comboBonus", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException(
                        $"Skill {skillId}: comboBonus was removed from the skill contract. Put extra effects in effectsOnHit.");
                }
            }
        }
    }

    private static void ValidateSkills(IEnumerable<SkillDefinition> skills)
    {
        foreach (var skill in skills)
        {
            if (string.IsNullOrWhiteSpace(skill.Id))
            {
                throw new InvalidDataException("Skill id is required.");
            }

            if (skill.BaseDamage.Min > skill.BaseDamage.Max)
            {
                throw new InvalidDataException($"Skill {skill.Id} has invalid damage range.");
            }

            if (double.IsNaN(skill.CorruptionCost) || double.IsInfinity(skill.CorruptionCost))
            {
                throw new InvalidDataException($"Skill {skill.Id}: corruptionCost must be a finite number.");
            }

            if (!Enum.IsDefined(typeof(SkillTargetKind), skill.TargetKind))
            {
                throw new InvalidDataException($"Skill {skill.Id}: unknown targetKind.");
            }

            foreach (var effect in skill.EffectsOnHit)
            {
                if (!Enum.IsDefined(typeof(EffectScope), effect.EffectScope))
                {
                    throw new InvalidDataException($"Skill {skill.Id}: unknown effectScope.");
                }
            }
        }
    }

    private static void ValidateEnemies(IEnumerable<EnemyDefinition> enemies)
    {
        foreach (var enemy in enemies)
        {
            if (enemy.Size is < 1 or > 3)
            {
                throw new InvalidDataException($"Enemy {enemy.Id} has invalid size.");
            }
        }
    }

    private static void ValidateSkillTrees(IEnumerable<CharacterSkillTreesDefinition> trees)
    {
        foreach (var character in trees)
        {
            foreach (var tree in character.Trees)
            {
                foreach (var tier in tree.Tiers)
                {
                    if (tier.Nodes.Count != 4)
                    {
                        throw new InvalidDataException($"Tree {tree.Element} tier {tier.Tier} must have 4 nodes.");
                    }
                }
            }
        }
    }

    private static void ValidatePassives(IReadOnlyList<PassiveDefinition> passives)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var passiveDefinition in passives)
        {
            if (string.IsNullOrWhiteSpace(passiveDefinition.Id))
            {
                throw new InvalidDataException("Passive id is required.");
            }

            if (!seen.Add(passiveDefinition.Id))
            {
                throw new InvalidDataException($"Duplicate passive id: {passiveDefinition.Id}");
            }
        }
    }

    private static void ValidateItems(IReadOnlyList<CombatItemDefinition> items)
    {
        var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                throw new InvalidDataException("Item id is required.");
            }

            if (!seenItemIds.Add(item.Id))
            {
                throw new InvalidDataException($"Duplicate item id: {item.Id}");
            }

            if (item.Kind != CombatItemKind.Utility && (item.StatModifiers == null || item.StatModifiers.Count == 0))
            {
                throw new InvalidDataException($"Item {item.Id} needs at least one stat modifier.");
            }

            if (item.Kind == CombatItemKind.Utility && item.UtilityKind == CombatItemUtilityKind.None)
            {
                throw new InvalidDataException($"Utility item {item.Id} needs a utilityKind.");
            }
        }
    }
}

public static class SampleCombatData
{
    /// <summary>Loads the canonical <c>skills.json</c> from <see cref="CombatDataLoader.ResolveDefaultSkillsPath"/>.</summary>
    public static IReadOnlyList<SkillDefinition> CreateSkills() =>
        CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath());

    /// <summary>Loads <c>passives.json</c> quando existir; caso contrário lista vazia (combate sem catálogo de passivas).</summary>
    public static IReadOnlyList<PassiveDefinition> CreatePassives()
    {
        try
        {
            return CombatDataLoader.LoadPassives(CombatDataLoader.ResolveDefaultPassivesPath());
        }
        catch (FileNotFoundException)
        {
            return [];
        }
    }

    /// <summary>Loads the canonical <c>items.json</c> from StreamingAssets.</summary>
    public static IReadOnlyList<CombatItemDefinition> CreateItems() =>
        CombatDataLoader.LoadItems(CombatDataLoader.ResolveDefaultItemsPath());
}
