using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Resolve a party de combate (Main + Companion) a partir do overworld, snapshots ou cache da entrada em combate.
/// </summary>
public static class CombatPartyResolver
{
    private const string DefaultMainCharacterName = "Wulfric";
    private const string DefaultCompanionCharacterName = "Buck";
    private const string IgnoredCombatCharacterName = "Matsuda";

    private static readonly string[] FallbackCombatAllyCharacterNames =
        { DefaultMainCharacterName, DefaultCompanionCharacterName };

    public static bool TryGetCombatPartyNames(
        PlayableCharactersManager playableCharactersManager,
        out string mainCharacterName,
        out string companionCharacterName)
    {
        mainCharacterName = null;
        companionCharacterName = null;

        if (playableCharactersManager == null)
        {
            return false;
        }

        if (playableCharactersManager.Main is PlayableCharacter mainPlayable)
        {
            mainCharacterName = mainPlayable.CharacterName;
        }

        if (playableCharactersManager.Companion is PlayableCharacter companionPlayable)
        {
            companionCharacterName = companionPlayable.CharacterName;
        }

        return !string.IsNullOrWhiteSpace(mainCharacterName);
    }

    public static IReadOnlyList<string> GetCombatAllyCharacterNames(PlayableCharactersManager playableCharactersManager = null)
    {
        var pendingCombatParty = CombatExplorationBridge.Instance?.TryGetPendingCombatAllyCharacterNames();
        if (pendingCombatParty != null && pendingCombatParty.Count > 0)
        {
            return pendingCombatParty;
        }

        if (IsCombatSceneActive())
        {
            var partyFromSnapshots = TryGetPartyFromExplorationSnapshots();
            if (partyFromSnapshots.Count > 0)
            {
                return NormalizeCombatParty(partyFromSnapshots);
            }
        }

        playableCharactersManager ??= UnityEngine.Object.FindFirstObjectByType<PlayableCharactersManager>();

        if (TryGetCombatPartyNames(playableCharactersManager, out var mainCharacterName, out var companionCharacterName))
        {
            var partyFromManager = BuildOrderedPartyList(mainCharacterName, companionCharacterName);
            if (partyFromManager.Count > 0)
            {
                return NormalizeCombatParty(partyFromManager);
            }
        }

        var fallbackPartyFromSnapshots = TryGetPartyFromExplorationSnapshots();
        if (fallbackPartyFromSnapshots.Count > 0)
        {
            return NormalizeCombatParty(fallbackPartyFromSnapshots);
        }

        return NormalizeCombatParty(FallbackCombatAllyCharacterNames);
    }

    public static IReadOnlyList<string> BuildPartyNamesFromSnapshots(IReadOnlyList<PlayableCharacterSnapshot> snapshots)
    {
        if (snapshots == null || snapshots.Count == 0)
        {
            return Array.Empty<string>();
        }

        string mainCharacterName = null;
        string companionCharacterName = null;

        for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
        {
            var snapshot = snapshots[snapshotIndex];
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.CharacterName))
            {
                continue;
            }

            if (snapshot.State == PlayableCharacterState.Main)
            {
                mainCharacterName = snapshot.CharacterName;
            }
            else if (snapshot.State == PlayableCharacterState.Companion)
            {
                companionCharacterName = snapshot.CharacterName;
            }
        }

        return BuildOrderedPartyList(mainCharacterName, companionCharacterName);
    }

    public static IReadOnlyList<string> NormalizeCombatParty(IReadOnlyList<string> rawPartyCharacterNames)
    {
        string mainCharacterName = null;
        string companionCharacterName = null;

        if (rawPartyCharacterNames != null)
        {
            for (var partyIndex = 0; partyIndex < rawPartyCharacterNames.Count; partyIndex++)
            {
                var candidateCharacterName = rawPartyCharacterNames[partyIndex];
                if (ShouldIgnoreCombatCharacter(candidateCharacterName))
                {
                    continue;
                }

                if (string.Equals(
                        candidateCharacterName,
                        DefaultMainCharacterName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    mainCharacterName = DefaultMainCharacterName;
                    continue;
                }

                if (companionCharacterName == null &&
                    !string.Equals(
                        candidateCharacterName,
                        DefaultMainCharacterName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    companionCharacterName = candidateCharacterName;
                }
            }
        }

        mainCharacterName ??= DefaultMainCharacterName;

        if (string.IsNullOrWhiteSpace(companionCharacterName) ||
            ShouldIgnoreCombatCharacter(companionCharacterName) ||
            string.Equals(companionCharacterName, mainCharacterName, StringComparison.OrdinalIgnoreCase))
        {
            companionCharacterName = DefaultCompanionCharacterName;
        }

        return new[]
        {
            mainCharacterName,
            companionCharacterName,
        };
    }

    private static bool ShouldIgnoreCombatCharacter(string characterName)
    {
        return string.Equals(characterName, IgnoredCombatCharacterName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCombatSceneActive()
    {
        var activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() &&
               activeScene.name.IndexOf("Combat", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static List<string> TryGetPartyFromExplorationSnapshots()
    {
        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext == null)
        {
            return new List<string>();
        }

        var partyFromSnapshots = loadContext.GetCombatAllyCharacterNamesFromSnapshots();
        return partyFromSnapshots.Count > 0
            ? new List<string>(partyFromSnapshots)
            : new List<string>();
    }

    private static List<string> BuildOrderedPartyList(string mainCharacterName, string companionCharacterName)
    {
        var partyCharacterNames = new List<string>(2);

        if (!string.IsNullOrWhiteSpace(mainCharacterName))
        {
            partyCharacterNames.Add(mainCharacterName);
        }

        if (!string.IsNullOrWhiteSpace(companionCharacterName))
        {
            partyCharacterNames.Add(companionCharacterName);
        }

        return partyCharacterNames;
    }
}
