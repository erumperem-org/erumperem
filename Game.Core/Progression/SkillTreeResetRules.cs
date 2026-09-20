using Game.Core.Domain;

namespace Game.Core.Progression;

/// <summary>
/// Utility combat items reset skill-tree unlocks. Spec "Matsuda" maps to characterId <see cref="MariaCharacterId"/>.
/// </summary>
public static class SkillTreeResetRules
{
    public const string WulfricCharacterId = "wulfric";
    public const string BuckCharacterId = "buck";
    public const string MariaCharacterId = "maria";

    public static readonly IReadOnlyList<string> AllPlayableCharacterIds =
    [
        WulfricCharacterId,
        BuckCharacterId,
        MariaCharacterId,
    ];

    public static IReadOnlyList<string> ResolveCharacterIdsToReset(CombatItemUtilityKind utilityKind) =>
        utilityKind switch
        {
            CombatItemUtilityKind.ResetAllSkillTrees => AllPlayableCharacterIds,
            CombatItemUtilityKind.ResetBuckSkillTree => [BuckCharacterId],
            CombatItemUtilityKind.ResetWulfricSkillTree => [WulfricCharacterId],
            CombatItemUtilityKind.ResetMariaSkillTree => [MariaCharacterId],
            _ => [],
        };

    public static Dictionary<string, Dictionary<string, bool>> ResetUnlockedNodes(
        IReadOnlyDictionary<string, Dictionary<string, bool>> unlockedNodesByCharacterId,
        CombatItemUtilityKind utilityKind)
    {
        var characterIdsToReset = new HashSet<string>(
            ResolveCharacterIdsToReset(utilityKind),
            StringComparer.OrdinalIgnoreCase);

        var resetNodes = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
        if (unlockedNodesByCharacterId == null)
        {
            return resetNodes;
        }

        foreach (var characterEntry in unlockedNodesByCharacterId)
        {
            if (characterIdsToReset.Contains(characterEntry.Key))
            {
                continue;
            }

            resetNodes[characterEntry.Key] = new Dictionary<string, bool>(
                characterEntry.Value ?? new Dictionary<string, bool>(),
                StringComparer.Ordinal);
        }

        return resetNodes;
    }
}
