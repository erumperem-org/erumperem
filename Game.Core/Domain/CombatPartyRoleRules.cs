using System.Linq;

namespace Game.Core.Domain;

/// <summary>
/// Overworld Main/Companion maps to <see cref="CombatantPartyRole"/>. Combat roster order is identity-stable
/// so Wulfric/Buck kits stay on those characters when the village swap changes who is Main.
/// </summary>
public static class CombatPartyRoleRules
{
    public const string DefaultLeaderCharacterName = "Wulfric";
    public const string DefaultCompanionCharacterName = "Buck";
    public const string IgnoredExplorationCharacterName = "Matsuda";

    public static CombatantPartyRole FromAllyPartyIndex(int partyIndex) =>
        partyIndex == 0 ? CombatantPartyRole.Leader : CombatantPartyRole.Companion;

    public static bool PassiveAppliesToCombatant(
        PassiveRequiredPartyRole requiredPartyRole,
        CombatantPartyRole combatantPartyRole)
    {
        return requiredPartyRole switch
        {
            PassiveRequiredPartyRole.Any => true,
            PassiveRequiredPartyRole.Leader => combatantPartyRole == CombatantPartyRole.Leader,
            PassiveRequiredPartyRole.Companion => combatantPartyRole == CombatantPartyRole.Companion,
            _ => true,
        };
    }

    public static bool ShouldIgnoreExplorationCharacter(string? characterName) =>
        string.Equals(characterName, IgnoredExplorationCharacterName, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// First non-ignored name is Leader; the next distinct name is Companion.
    /// Matsuda never enters combat. Empty slots fall back to Wulfric / Buck without swapping an existing Main.
    /// </summary>
    public static CombatantPartyRole FromOverworldPartyNames(
        string characterName,
        string? leaderCharacterName)
    {
        if (string.IsNullOrWhiteSpace(characterName) || string.IsNullOrWhiteSpace(leaderCharacterName))
        {
            return CombatantPartyRole.Companion;
        }

        return string.Equals(characterName, leaderCharacterName, StringComparison.OrdinalIgnoreCase)
            ? CombatantPartyRole.Leader
            : CombatantPartyRole.Companion;
    }

    public static IReadOnlyList<string> NormalizeOverworldCombatParty(IReadOnlyList<string>? rawPartyCharacterNames)
    {
        string? leaderCharacterName = null;
        string? companionCharacterName = null;

        if (rawPartyCharacterNames != null)
        {
            for (var partyIndex = 0; partyIndex < rawPartyCharacterNames.Count; partyIndex++)
            {
                var candidateCharacterName = rawPartyCharacterNames[partyIndex];
                if (string.IsNullOrWhiteSpace(candidateCharacterName) ||
                    ShouldIgnoreExplorationCharacter(candidateCharacterName))
                {
                    continue;
                }

                if (leaderCharacterName == null)
                {
                    leaderCharacterName = candidateCharacterName;
                    continue;
                }

                if (companionCharacterName == null &&
                    !string.Equals(candidateCharacterName, leaderCharacterName, StringComparison.OrdinalIgnoreCase))
                {
                    companionCharacterName = candidateCharacterName;
                }
            }
        }

        leaderCharacterName ??= DefaultLeaderCharacterName;

        if (string.IsNullOrWhiteSpace(companionCharacterName) ||
            ShouldIgnoreExplorationCharacter(companionCharacterName) ||
            string.Equals(companionCharacterName, leaderCharacterName, StringComparison.OrdinalIgnoreCase))
        {
            companionCharacterName = string.Equals(
                    leaderCharacterName,
                    DefaultCompanionCharacterName,
                    StringComparison.OrdinalIgnoreCase)
                ? DefaultLeaderCharacterName
                : DefaultCompanionCharacterName;
        }

        return [leaderCharacterName, companionCharacterName];
    }

    /// <summary>
    /// Combat roster order is identity-stable (Wulfric, Buck, Maria) so kits and visuals
    /// follow the character, not the Main/Companion slot. PartyRole still comes from overworld Main.
    /// </summary>
    public static IReadOnlyList<string> SortIdentityStableCombatRoster(IReadOnlyList<string> partyCharacterNames)
    {
        if (partyCharacterNames == null || partyCharacterNames.Count == 0)
        {
            return NormalizeOverworldCombatParty(null);
        }

        var canonicalOrder = new[]
        {
            DefaultLeaderCharacterName,
            DefaultCompanionCharacterName,
            "Maria",
        };

        var remainingNames = partyCharacterNames
            .Where(characterName => !string.IsNullOrWhiteSpace(characterName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var orderedNames = new List<string>(remainingNames.Count);

        foreach (var canonicalName in canonicalOrder)
        {
            var matchedName = remainingNames.FirstOrDefault(characterName =>
                string.Equals(characterName, canonicalName, StringComparison.OrdinalIgnoreCase));
            if (matchedName == null)
            {
                continue;
            }

            orderedNames.Add(matchedName);
            remainingNames.RemoveAll(characterName =>
                string.Equals(characterName, canonicalName, StringComparison.OrdinalIgnoreCase));
        }

        remainingNames.Sort(StringComparer.OrdinalIgnoreCase);
        orderedNames.AddRange(remainingNames);
        return orderedNames.Count > 0 ? orderedNames : NormalizeOverworldCombatParty(null);
    }
}
