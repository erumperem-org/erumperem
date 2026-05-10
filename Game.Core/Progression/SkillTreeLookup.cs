using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Progression;

/// <summary>Queries node placement and derived loadouts from <see cref="CharacterSkillTreesDefinition"/>.</summary>
public static class SkillTreeLookup
{
    public static bool TryFindNode(
        CharacterSkillTreesDefinition character,
        string nodeId,
        out ElementType elementType,
        out SkillTreeNodeDefinition nodeDefinition)
    {
        elementType = default;
        nodeDefinition = null!;

        foreach (var tree in character.Trees)
        {
            foreach (var tier in tree.Tiers)
            {
                foreach (var node in tier.Nodes)
                {
                    if (node.Id != nodeId) continue;

                    elementType = tree.Element;
                    nodeDefinition = node;
                    return true;
                }
            }
        }

        return false;
    }

    public static int SumUnlockedNodeCosts(
        CharacterSkillTreesDefinition character,
        IReadOnlyDictionary<string, bool> unlockedNodes)
    {
        var sum = 0;
        foreach (var tree in character.Trees)
        {
            foreach (var tier in tree.Tiers)
            {
                foreach (var node in tier.Nodes)
                {
                    if (unlockedNodes.TryGetValue(node.Id, out var isOn) && isOn)
                    {
                        sum += node.Cost;
                    }
                }
            }
        }

        return sum;
    }

    /// <summary>Innate skills always included; active tree skills only if their node is unlocked.</summary>
    public static List<string> BuildPlayerSkillLoadout(
        CharacterSkillTreesDefinition character,
        IReadOnlyDictionary<string, bool> unlockedNodes,
        IReadOnlyList<string> innateSkillIds)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<string>();

        void AddSkillId(string skillId)
        {
            if (string.IsNullOrWhiteSpace(skillId) || !seen.Add(skillId))
            {
                return;
            }

            ordered.Add(skillId);
        }

        foreach (var innateSkillId in innateSkillIds)
        {
            AddSkillId(innateSkillId);
        }

        foreach (var tree in character.Trees)
        {
            foreach (var tier in tree.Tiers)
            {
                foreach (var node in tier.Nodes)
                {
                    if (!string.Equals(node.Type, "Active", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!unlockedNodes.TryGetValue(node.Id, out var unlocked) || !unlocked)
                    {
                        continue;
                    }

                    AddSkillId(node.Id);
                }
            }
        }

        return ordered;
    }

    public static CharacterSkillTreesDefinition? FindCharacterTrees(
        IReadOnlyList<CharacterSkillTreesDefinition> catalog,
        string characterId)
    {
        foreach (var entry in catalog)
        {
            if (string.Equals(entry.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
    }
}
