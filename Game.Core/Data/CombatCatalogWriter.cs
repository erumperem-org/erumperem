using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Core.Domain;
using Game.Core.Items;
using Game.Core.Models;

namespace Game.Core.Data;

/// <summary>
/// Writes the combat JSON catalog under <c>Assets/StreamingAssets/Data</c> (skills, passives, skill trees, items).
/// Does not write <c>enemies.json</c>.
/// </summary>
public static class CombatCatalogWriter
{
    public static JsonSerializerOptions CreateWriteOptions() => new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters =
        {
            new SkillTargetKindJsonConverter(),
            new PassiveEffectKindJsonConverter(),
            new JsonStringEnumConverter(),
        },
    };

    public static void WriteSkills(string path, IReadOnlyList<SkillDefinition> skills) =>
        WriteJson(path, skills);

    public static void WritePassives(string path, IReadOnlyList<PassiveDefinition> passives) =>
        WriteJson(path, passives);

    public static void WriteSkillTrees(string path, IReadOnlyList<CharacterSkillTreesDefinition> skillTrees) =>
        WriteJson(path, skillTrees);

    public static void WriteItems(string path, IReadOnlyList<CombatItemDefinition> items) =>
        WriteJson(path, items);

    public static IReadOnlyList<SkillDefinition> UpsertSkill(
        IReadOnlyList<SkillDefinition> existingSkills,
        SkillDefinition incomingSkill) =>
        UpsertById(existingSkills, incomingSkill, skill => skill.Id);

    public static IReadOnlyList<PassiveDefinition> UpsertPassive(
        IReadOnlyList<PassiveDefinition> existingPassives,
        PassiveDefinition incomingPassive) =>
        UpsertById(existingPassives, incomingPassive, passive => passive.Id);

    public static IReadOnlyList<CombatItemDefinition> UpsertItem(
        IReadOnlyList<CombatItemDefinition> existingItems,
        CombatItemDefinition incomingItem) =>
        UpsertById(existingItems, incomingItem, item => item.Id);

    /// <summary>
    /// Replaces a tree node with the same id, or inserts when the tier still has fewer than 4 nodes.
    /// Returns false when the character/tree/tier cannot accept the node (does not invent kits).
    /// </summary>
    public static bool TryUpsertSkillTreeNode(
        IList<CharacterSkillTreesDefinition> characters,
        string ownerCharacterId,
        int treeIndexOneBased,
        int tierIndex,
        SkillTreeNodeDefinition incomingNode,
        out string failureMessage)
    {
        if (string.IsNullOrWhiteSpace(ownerCharacterId))
        {
            failureMessage = "Tree node is missing ownerCharacterId.";
            return false;
        }

        var characterIndex = -1;
        for (var index = 0; index < characters.Count; index++)
        {
            if (string.Equals(characters[index].CharacterId, ownerCharacterId, StringComparison.OrdinalIgnoreCase))
            {
                characterIndex = index;
                break;
            }
        }

        if (characterIndex < 0)
        {
            failureMessage =
                $"No skill-tree character '{ownerCharacterId}'. Export will still write skills/passives; add the character to skill_trees.json before placing TreeNode abilities.";
            return false;
        }

        var character = characters[characterIndex];
        var treeIndexZeroBased = treeIndexOneBased - 1;
        if (treeIndexZeroBased < 0 || treeIndexZeroBased >= character.Trees.Count)
        {
            failureMessage =
                $"Character '{ownerCharacterId}' has no tree at treeIndex {treeIndexOneBased}.";
            return false;
        }

        var tree = character.Trees[treeIndexZeroBased];
        var tierIndexInTree = -1;
        for (var index = 0; index < tree.Tiers.Count; index++)
        {
            if (tree.Tiers[index].Tier == tierIndex)
            {
                tierIndexInTree = index;
                break;
            }
        }

        if (tierIndexInTree < 0)
        {
            failureMessage =
                $"Character '{ownerCharacterId}' tree {treeIndexOneBased} has no tier {tierIndex}.";
            return false;
        }

        var tier = tree.Tiers[tierIndexInTree];
        var nodes = tier.Nodes.ToList();
        var existingNodeIndex = nodes.FindIndex(node =>
            string.Equals(node.Id, incomingNode.Id, StringComparison.Ordinal));

        if (existingNodeIndex >= 0)
        {
            nodes[existingNodeIndex] = incomingNode;
        }
        else if (nodes.Count < 4)
        {
            nodes.Add(incomingNode);
        }
        else
        {
            failureMessage =
                $"Tier {tierIndex} of '{ownerCharacterId}' tree {treeIndexOneBased} already has 4 nodes. Change abilityId to an existing node id or free a slot.";
            return false;
        }

        var replacedTiers = tree.Tiers.ToList();
        replacedTiers[tierIndexInTree] = new SkillTreeTierDefinition
        {
            Tier = tier.Tier,
            Nodes = nodes,
        };

        var replacedTrees = character.Trees.ToList();
        replacedTrees[treeIndexZeroBased] = new SkillTreeDefinition
        {
            Element = tree.Element,
            Tiers = replacedTiers,
        };

        characters[characterIndex] = new CharacterSkillTreesDefinition
        {
            CharacterId = character.CharacterId,
            Trees = replacedTrees,
        };

        failureMessage = string.Empty;
        return true;
    }

    private static IReadOnlyList<TItem> UpsertById<TItem>(
        IReadOnlyList<TItem> existingItems,
        TItem incomingItem,
        Func<TItem, string> readId)
    {
        var mergedItems = existingItems.ToList();
        var incomingId = readId(incomingItem);
        var existingIndex = mergedItems.FindIndex(item =>
            string.Equals(readId(item), incomingId, StringComparison.Ordinal));

        if (existingIndex >= 0)
        {
            mergedItems[existingIndex] = incomingItem;
        }
        else
        {
            mergedItems.Add(incomingItem);
        }

        return mergedItems;
    }

    private static void WriteJson<TValue>(string path, TValue value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, CreateWriteOptions());
        File.WriteAllText(path, json + Environment.NewLine);
    }
}
