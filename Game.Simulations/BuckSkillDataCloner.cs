using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Game.Simulations;

/// <summary>One-shot utility: clones Wulfric skill tree JSON into Buck placeholder entries.</summary>
public static class BuckSkillDataCloner
{
    private static readonly JsonSerializerOptions JsonWriteOptions = new() { WriteIndented = true };

    public static void Run(string simulationsDataDirectory, string streamingAssetsDataDirectory)
    {
        CloneSkillTrees(simulationsDataDirectory, streamingAssetsDataDirectory);
        CloneSkills(simulationsDataDirectory, streamingAssetsDataDirectory);
        ClonePassives(simulationsDataDirectory, streamingAssetsDataDirectory);
        Console.WriteLine("Buck skill data cloned to Simulations and StreamingAssets.");
    }

    private static void CloneSkillTrees(string simulationsDataDirectory, string streamingAssetsDataDirectory)
    {
        var path = Path.Combine(simulationsDataDirectory, "skill_trees.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        if (root.Any(node => string.Equals(node?["characterId"]?.GetValue<string>(), "buck", StringComparison.OrdinalIgnoreCase)))
        {
            WriteToBoth(path, simulationsDataDirectory, streamingAssetsDataDirectory, root);
            return;
        }

        var wulfricNode = root.FirstOrDefault(node =>
            string.Equals(node?["characterId"]?.GetValue<string>(), "wulfric", StringComparison.OrdinalIgnoreCase));
        if (wulfricNode == null)
        {
            throw new InvalidOperationException("skill_trees.json: missing wulfric entry.");
        }

        var buckNode = wulfricNode.DeepClone();
        buckNode!["characterId"] = "buck";
        RemapSkillTreeNodeIds(buckNode);
        root.Add(buckNode);
        WriteToBoth(path, simulationsDataDirectory, streamingAssetsDataDirectory, root);
    }

    private static void RemapSkillTreeNodeIds(JsonNode? characterNode)
    {
        foreach (var treeNode in characterNode?["trees"]?.AsArray() ?? Enumerable.Empty<JsonNode?>())
        {
            foreach (var tierNode in treeNode?["tiers"]?.AsArray() ?? Enumerable.Empty<JsonNode?>())
            {
                foreach (var skillNode in tierNode?["nodes"]?.AsArray() ?? Enumerable.Empty<JsonNode?>())
                {
                    if (skillNode?["id"] is JsonValue idValue)
                    {
                        skillNode["id"] = RemapTreeNodeId(idValue.GetValue<string>());
                    }

                    if (skillNode?["requires"] is JsonArray requiresArray)
                    {
                        for (var requireIndex = 0; requireIndex < requiresArray.Count; requireIndex++)
                        {
                            requiresArray[requireIndex] = RemapTreeNodeId(requiresArray[requireIndex]!.GetValue<string>());
                        }
                    }
                }
            }
        }
    }

    private static void CloneSkills(string simulationsDataDirectory, string streamingAssetsDataDirectory)
    {
        var path = Path.Combine(simulationsDataDirectory, "skills.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        var existingIds = root
            .Select(node => node?["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sourceSkills = root
            .Where(node =>
            {
                var skillId = node?["id"]?.GetValue<string>();
                return skillId != null &&
                       (skillId.StartsWith("wulfric_innate_", StringComparison.Ordinal) ||
                        TreeSkillIdPattern.IsMatch(skillId));
            })
            .ToList();

        foreach (var sourceSkill in sourceSkills)
        {
            var sourceId = sourceSkill["id"]!.GetValue<string>();
            var buckId = RemapSkillId(sourceId);
            if (existingIds.Contains(buckId))
            {
                continue;
            }

            var buckSkill = sourceSkill.DeepClone();
            buckSkill["id"] = buckId;
            ApplyBuckSkillPresentationTweaks(buckSkill);
            root.Add(buckSkill);
            existingIds.Add(buckId);
        }

        WriteToBoth(path, simulationsDataDirectory, streamingAssetsDataDirectory, root);
    }

    private static void ClonePassives(string simulationsDataDirectory, string streamingAssetsDataDirectory)
    {
        var path = Path.Combine(simulationsDataDirectory, "passives.json");
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsArray();
        var existingIds = root
            .Select(node => node?["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var sourcePassive in root.ToList())
        {
            var passiveId = sourcePassive?["id"]?.GetValue<string>();
            if (passiveId == null || passiveId.StartsWith("b_", StringComparison.Ordinal) || !TreeSkillIdPattern.IsMatch(passiveId))
            {
                continue;
            }

            var buckPassiveId = RemapTreeNodeId(passiveId);
            if (existingIds.Contains(buckPassiveId))
            {
                continue;
            }

            var buckPassive = sourcePassive!.DeepClone();
            buckPassive["id"] = buckPassiveId;
            RemapPassiveReferences(buckPassive);
            root.Add(buckPassive);
            existingIds.Add(buckPassiveId);
        }

        WriteToBoth(path, simulationsDataDirectory, streamingAssetsDataDirectory, root);
    }

    private static void RemapPassiveReferences(JsonNode passiveNode)
    {
        foreach (var propertyName in new[] { "skillId", "prerequisiteSkillId" })
        {
            if (passiveNode[propertyName]?.GetValue<string>() is { } skillReference)
            {
                passiveNode[propertyName] = RemapSkillId(skillReference);
            }
        }

        if (string.Equals(passiveNode["dotType"]?.GetValue<string>(), "Bleed", StringComparison.OrdinalIgnoreCase))
        {
            passiveNode["dotType"] = "Burn";
        }
    }

    private static void ApplyBuckSkillPresentationTweaks(JsonNode skillNode)
    {
        if (skillNode["name"]?.GetValue<string>() is { } skillName)
        {
            skillNode["name"] = skillName switch
            {
                "Rasgar tendão" => "Tiro incendiário",
                "Fio candente" => "Rajada flamejante",
                "Execução de leilão" => "Execução do pistoleiro",
                "Remendar couraça" => "Reforço de couro",
                "Muralha" => "Barricada",
                "Salvaguarda" => "Último recurso",
                "Fio da anomalia" => "Fio do revólver",
                "Puxar o véu" => "Puxar o gatilho",
                "Abrir o vão" => "Abrir fogo",
                "Talho direto" => "Disparo rápido",
                "Empurrão brutal" => "Empurrão do coldre",
                "Postura de lobo" => "Postura do duelista",
                _ => skillName,
            };
        }

        if (skillNode["baseDamage"] is JsonObject damageObject)
        {
            if (damageObject["min"] is JsonValue minValue)
            {
                damageObject["min"] = Math.Max(0, minValue.GetValue<int>() + 1);
            }

            if (damageObject["max"] is JsonValue maxValue)
            {
                var minDamage = damageObject["min"]?.GetValue<int>() ?? 0;
                damageObject["max"] = Math.Max(minDamage, maxValue.GetValue<int>() + 2);
            }
        }

        foreach (var effectListName in new[] { "effectsOnHit" })
        {
            if (skillNode[effectListName] is not JsonArray effectsArray)
            {
                continue;
            }

            foreach (var effectNode in effectsArray)
            {
                if (string.Equals(effectNode?["type"]?.GetValue<string>(), "ApplyDot", StringComparison.Ordinal) &&
                    string.Equals(effectNode?["dot"]?.GetValue<string>(), "Bleed", StringComparison.OrdinalIgnoreCase))
                {
                    effectNode!["dot"] = "Burn";
                }
            }
        }
    }

    private static string RemapTreeNodeId(string nodeId) =>
        nodeId.StartsWith("b_", StringComparison.Ordinal) ? nodeId : $"b_{nodeId}";

    private static string RemapSkillId(string skillId)
    {
        if (skillId.StartsWith("wulfric_innate_", StringComparison.Ordinal))
        {
            return skillId.Replace("wulfric_innate_", "buck_innate_", StringComparison.Ordinal);
        }

        if (TreeSkillIdPattern.IsMatch(skillId))
        {
            return RemapTreeNodeId(skillId);
        }

        return skillId;
    }

    private static void WriteToBoth(
        string simulationsFilePath,
        string simulationsDataDirectory,
        string streamingAssetsDataDirectory,
        JsonNode root)
    {
        var json = root.ToJsonString(JsonWriteOptions) + Environment.NewLine;
        File.WriteAllText(simulationsFilePath, json);
        var fileName = Path.GetFileName(simulationsFilePath);
        File.WriteAllText(Path.Combine(streamingAssetsDataDirectory, fileName), json);
    }

    private static readonly Regex TreeSkillIdPattern = new(@"^[fma]_t\d+_", RegexOptions.Compiled);
}
