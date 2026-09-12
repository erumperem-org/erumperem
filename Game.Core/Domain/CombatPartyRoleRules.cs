namespace Game.Core.Domain;

/// <summary>
/// Single mapping from overworld party order to combat <see cref="CombatantPartyRole"/>.
/// Index 0 is always Leader (overworld Main); index 1 is Companion. No name-based overrides.
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
}
