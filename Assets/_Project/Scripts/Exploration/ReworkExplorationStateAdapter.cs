using System;
using System.Collections.Generic;
using BarSystem.Bars.Corruption;
using UnityEngine;

/// <summary>
/// Adapta o overworld rework (<see cref="PlayableCharacterController"/> +
/// <see cref="BarSystem.Bars.Corruption.CorruptionBarInstaller"/>) ao ciclo
/// de save/load central usado pelo combate (<see cref="ExplorationLoadContext"/>).
/// </summary>
public static class ReworkExplorationStateAdapter
{
    public static bool TryFindReworkController(out PlayableCharacterController playableCharacterController)
    {
        playableCharacterController = UnityEngine.Object.FindFirstObjectByType<PlayableCharacterController>();
        return playableCharacterController != null;
    }

    public static bool TryCaptureSnapshotsFromReworkController(
        PlayableCharacterController playableCharacterController,
        Func<string, float> resolveMaxHealth,
        List<PlayableCharacterSnapshot> snapshotDestination)
    {
        if (playableCharacterController == null || snapshotDestination == null)
        {
            return false;
        }

        snapshotDestination.Clear();

        foreach (var playableCharacter in playableCharacterController.Roster)
        {
            if (playableCharacter == null || string.IsNullOrWhiteSpace(playableCharacter.CharacterId))
            {
                continue;
            }

            var characterName = playableCharacter.CharacterId;
            var maxHealth = resolveMaxHealth(characterName);
            var currentHealth = maxHealth;

            if (playableCharacter.HealthBar?.Model != null)
            {
                currentHealth = Mathf.Clamp(
                    playableCharacter.HealthBar.Model.Current,
                    0f,
                    maxHealth);
            }

            snapshotDestination.Add(new PlayableCharacterSnapshot(
                characterName,
                playableCharacter.transform.position,
                playableCharacter.transform.rotation,
                MapReworkStateToExplorationState(playableCharacter.CurrentState),
                currentHealth));
        }

        PatchMainAndCompanionSnapshotStatesFromController(playableCharacterController, snapshotDestination);

        return snapshotDestination.Count > 0;
    }

    private static void PatchMainAndCompanionSnapshotStatesFromController(
        PlayableCharacterController playableCharacterController,
        List<PlayableCharacterSnapshot> snapshots)
    {
        if (playableCharacterController == null || snapshots == null || snapshots.Count == 0)
        {
            return;
        }

        for (var snapshotIndex = 0; snapshotIndex < snapshots.Count; snapshotIndex++)
        {
            var snapshot = snapshots[snapshotIndex];
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.CharacterName))
            {
                continue;
            }

            if (string.Equals(
                    snapshot.CharacterName,
                    playableCharacterController.InGameCharacterId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                snapshot.State = PlayableCharacterState.Main;
                continue;
            }

            if (string.Equals(
                    snapshot.CharacterName,
                    playableCharacterController.CompanionCharacterId,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                snapshot.State = PlayableCharacterState.Companion;
                continue;
            }

            snapshot.State = PlayableCharacterState.Resting;
        }
    }

    public static bool TryResolveReworkCorruptionValue(out float corruptionValue)
    {
        var corruptionBarInstaller = UnityEngine.Object.FindFirstObjectByType<CorruptionBarInstaller>();
        if (corruptionBarInstaller?.Model != null)
        {
            corruptionValue = corruptionBarInstaller.Model.Current;
            return true;
        }

        corruptionValue = 0f;
        return false;
    }

    public static void TryApplySavedCorruptionToReworkBar(float corruptionValue)
    {
        var corruptionBarInstaller = UnityEngine.Object.FindFirstObjectByType<CorruptionBarInstaller>();
        if (corruptionBarInstaller?.Model == null)
        {
            return;
        }

        corruptionBarInstaller.Model.SetCurrent(
            Mathf.Clamp(corruptionValue, corruptionBarInstaller.Model.Min, corruptionBarInstaller.Model.Max));
    }

    public static bool IsReworkExplorationSceneActive()
    {
        return TryFindReworkController(out _);
    }

    public static PlayableCharacterState MapReworkStateToExplorationState(CharacterState characterState)
    {
        return characterState switch
        {
            CharacterState.InGame => PlayableCharacterState.Main,
            CharacterState.Companion => PlayableCharacterState.Companion,
            CharacterState.Resting => PlayableCharacterState.Resting,
            _ => PlayableCharacterState.Resting,
        };
    }

    public static bool TryGetReworkPartyCharacterNames(
        PlayableCharacterController playableCharacterController,
        out string mainCharacterName,
        out string companionCharacterName)
    {
        mainCharacterName = playableCharacterController?.InGameCharacterId;
        companionCharacterName = playableCharacterController?.CompanionCharacterId;
        return !string.IsNullOrWhiteSpace(mainCharacterName);
    }
}
