using Game.Core.Models;

namespace Game.Core.Progression;

public static class SkillTreeRules
{
    public static bool CanUnlockNode(
        CharacterSkillTreesDefinition treesDefinition,
        string elementName,
        string nodeId,
        IReadOnlyDictionary<string, bool> unlockedNodes)
    {
        var tree = treesDefinition.Trees.FirstOrDefault(elementTree =>
            string.Equals(elementTree.Element.ToString(), elementName, StringComparison.OrdinalIgnoreCase));
        if (tree is null) return false;

        var tierWithNode = tree.Tiers.FirstOrDefault(tier =>
            tier.Nodes.Any(nodeDefinition => nodeDefinition.Id == nodeId));
        if (tierWithNode is null) return false;

        var node = tierWithNode.Nodes.First(nodeDefinition => nodeDefinition.Id == nodeId);
        foreach (var requiredNode in node.Requires)
        {
            if (!unlockedNodes.TryGetValue(requiredNode, out var isUnlocked) || !isUnlocked)
            {
                return false;
            }
        }

        if (tierWithNode.Tier > 1)
        {
            var previousTier = tree.Tiers.First(tier => tier.Tier == tierWithNode.Tier - 1);
            var allPreviousUnlocked = previousTier.Nodes.All(nodeDefinition =>
                unlockedNodes.TryGetValue(nodeDefinition.Id, out var isUnlocked) && isUnlocked);
            if (!allPreviousUnlocked)
            {
                return false;
            }
        }

        return true;
    }
}
