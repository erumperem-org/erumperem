using Game.Core.Abstractions;
using Game.Core.Models;
using Game.Core.Progression;

namespace Game.Simulations;

/// <summary>
/// Helpers para presets de simulação alinhados a <c>skill_trees.json</c> (Wulfric: árvore 1 = Fogo, 2 = Metal, 3 = Anomalia).
/// </summary>
public static class SimulationSkillTreeSetup
{
    public const string DefaultCharacterId = "wulfric";

    public static CharacterSkillTreesDefinition GetCharacter(
        IReadOnlyList<CharacterSkillTreesDefinition> roots,
        string characterId = DefaultCharacterId)
    {
        var characterTrees = roots.FirstOrDefault(root => root.CharacterId == characterId);
        if (characterTrees is null)
        {
            throw new InvalidOperationException($"No skill tree for character '{characterId}'.");
        }

        return characterTrees;
    }

    /// <summary>Todos os nós (passivas + ativas) da árvore <paramref name="treeIndex1Based"/> até ao tier inclusive.</summary>
    public static IReadOnlyList<string> GetNodeIdsForTreeMaxTier(
        CharacterSkillTreesDefinition character,
        int treeIndex1Based,
        int maxTierInclusive)
    {
        if (treeIndex1Based < 1 || treeIndex1Based > character.Trees.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(treeIndex1Based), "Tree index must be 1..3 for Wulfric.");
        }

        if (maxTierInclusive < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTierInclusive));
        }

        var tree = character.Trees[treeIndex1Based - 1];
        return tree.Tiers
            .Where(tierDefinition => tierDefinition.Tier <= maxTierInclusive)
            .SelectMany(tierDefinition => tierDefinition.Nodes.Select(node => node.Id))
            .ToList();
    }

    /// <summary>Em cada uma das três árvores, todos os nós do tier 1 até <paramref name="maxTierInclusive"/>.</summary>
    public static IReadOnlyList<string> GetNodeIdsAllTreesMaxTier(
        CharacterSkillTreesDefinition character,
        int maxTierInclusive)
    {
        var ids = new List<string>();
        foreach (var tree in character.Trees)
        {
            foreach (var tier in tree.Tiers.Where(tierDefinition => tierDefinition.Tier <= maxTierInclusive))
            {
                foreach (var node in tier.Nodes)
                {
                    ids.Add(node.Id);
                }
            }
        }

        return ids;
    }

    public static SkillTreeNodeDefinition? FindNode(CharacterSkillTreesDefinition character, string nodeId)
    {
        foreach (var tree in character.Trees)
        {
            foreach (var tier in tree.Tiers)
            {
                foreach (var node in tier.Nodes)
                {
                    if (node.Id == nodeId)
                    {
                        return node;
                    }
                }
            }
        }

        return null;
    }

    public static int GetNodeCost(CharacterSkillTreesDefinition character, string nodeId) =>
        FindNode(character, nodeId)?.Cost ?? 1;

    public static IReadOnlyList<string> GetUnlockableNodeIds(
        CharacterSkillTreesDefinition treeDef,
        IReadOnlyDictionary<string, bool> unlockedSnapshot)
    {
        var list = new List<string>();
        foreach (var tree in treeDef.Trees)
        {
            var elementName = tree.Element.ToString();
            foreach (var tier in tree.Tiers)
            {
                foreach (var node in tier.Nodes)
                {
                    if (unlockedSnapshot.TryGetValue(node.Id, out var isNodeUnlocked) && isNodeUnlocked)
                    {
                        continue;
                    }

                    if (SkillTreeRules.CanUnlockNode(treeDef, elementName, node.Id, unlockedSnapshot))
                    {
                        list.Add(node.Id);
                    }
                }
            }
        }

        return list;
    }

    /// <summary>Desbloqueio válido por <see cref="SkillTreeRules"/>; orçamento aleatório 0..<paramref name="maxPoints"/>; nível = pontos gastos.</summary>
    public static void ApplyRandomTreeUnlocks(
        IEnumerable<Combatant> allies,
        CharacterSkillTreesDefinition treeDef,
        IRandomSource random,
        int maxPoints = 12)
    {
        if (!allies.Any())
        {
            return;
        }

        var budget = random.Next(0, maxPoints + 1);
        var unlocked = new Dictionary<string, bool>(StringComparer.Ordinal);
        var spent = 0;

        while (spent < budget)
        {
            var candidates = GetUnlockableNodeIds(treeDef, unlocked);
            if (candidates.Count == 0)
            {
                break;
            }

            var pick = candidates[random.Next(0, candidates.Count)];
            var cost = GetNodeCost(treeDef, pick);
            if (spent + cost > budget)
            {
                break;
            }

            unlocked[pick] = true;
            spent += cost;
        }

        foreach (var ally in allies)
        {
            foreach (var nodeIdAndUnlocked in unlocked)
            {
                ally.Progression.UnlockedNodes[nodeIdAndUnlocked.Key] = nodeIdAndUnlocked.Value;
            }

            ally.Progression.Level = spent;
            ally.Progression.SpentPoints = spent;
        }
    }

    public static void ApplyNodeUnlocks(
        IEnumerable<Combatant> allies,
        CharacterSkillTreesDefinition character,
        IEnumerable<string> nodeIds)
    {
        var ids = nodeIds.Distinct(StringComparer.Ordinal).ToList();
        var spent = ids.Sum(id => GetNodeCost(character, id));

        foreach (var ally in allies)
        {
            foreach (var nodeId in ids)
            {
                ally.Progression.UnlockedNodes[nodeId] = true;
            }

            ally.Progression.Level = spent;
            ally.Progression.SpentPoints = spent;
        }
    }
}
