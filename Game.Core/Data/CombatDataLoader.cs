using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Data;

public static class CombatDataLoader
{
    /// <summary>
    /// Locates <c>Game.Simulations/Data/skills.json</c> when running tests, simulations, or the IDE.
    /// </summary>
    public static string ResolveDefaultSkillsPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "skills.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "skills.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Game.Simulations", "Data", "skills.json")),
        };
        foreach (var candidatePath in candidates)
        {
            if (File.Exists(candidatePath)) return candidatePath;
        }

        throw new FileNotFoundException("skills.json not found. Tried: " + string.Join("; ", candidates));
    }

    /// <summary>
    /// Locates <c>Game.Simulations/Data/passives.json</c> (mesmo padrão que <see cref="ResolveDefaultSkillsPath"/>).
    /// </summary>
    public static string ResolveDefaultPassivesPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "passives.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "passives.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Game.Simulations", "Data", "passives.json")),
        };
        foreach (var candidatePath in candidates)
        {
            if (File.Exists(candidatePath)) return candidatePath;
        }

        throw new FileNotFoundException("passives.json not found. Tried: " + string.Join("; ", candidates));
    }

    /// <summary>Localiza <c>Game.Simulations/Data/skill_trees.json</c>.</summary>
    public static string ResolveDefaultSkillTreesPath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "skill_trees.json"),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Data", "skill_trees.json")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Game.Simulations", "Data", "skill_trees.json")),
        };
        foreach (var candidatePath in candidates)
        {
            if (File.Exists(candidatePath)) return candidatePath;
        }

        throw new FileNotFoundException("skill_trees.json not found. Tried: " + string.Join("; ", candidates));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<SkillDefinition> LoadSkills(string path)
    {
        var json = File.ReadAllText(path);
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
}
