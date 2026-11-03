using Game.Core.Domain;

namespace Game.Core.Progression;

/// <summary>
/// Utility combat items reset skill-tree unlocks. Combat characterId is <see cref="MatsudaCharacterId"/>.
/// </summary>
public static class SkillTreeResetRules
{
    public const string WulfricCharacterId = "wulfric";
    public const string BuckCharacterId = "buck";
    public const string MatsudaCharacterId = "matsuda";

    public static readonly IReadOnlyList<string> AllPlayableCharacterIds =
    [
        WulfricCharacterId,
        BuckCharacterId,
        MatsudaCharacterId,
    ];

    public static IReadOnlyList<string> ResolveCharacterIdsToReset(CombatItemUtilityKind utilityKind) =>
        utilityKind switch
        {
            CombatItemUtilityKind.ResetAllSkillTrees => AllPlayableCharacterIds,
            CombatItemUtilityKind.ResetBuckSkillTree => [BuckCharacterId],
            CombatItemUtilityKind.ResetWulfricSkillTree => [WulfricCharacterId],
            CombatItemUtilityKind.ResetMatsudaSkillTree => [MatsudaCharacterId],
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
