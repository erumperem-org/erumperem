using Player;
using UnityEngine;

[System.Serializable]
public class PlayableCharacterStatesBuilder
{
    [Header("Input (Main)")]
    public PlayerInputReader inputReader;

    [Header("Companion")]
    [Tooltip("Referência ao transform do Main atual — atualizada em BuildMainCharacter.")]
    public Transform mainTransform;

    // ── Build states ──────────────────────────────────────────────────────

    public void BuildMainCharacter(PlayableCharacter character)
    {
        ForceExitAllReceivers();

        mainTransform = character.transform;

        character.movementController.EnableMovement();
        character.detectionSystem.gameObject.tag = "Player";
        character.detectionSystem.Scan();

        if (inputReader != null)
            inputReader.playerDetectionSystem = character.detectionSystem;
    }
    // PlayableCharacterStatesBuilder
    public void BuildCompanionCharacter(PlayableCharacter character, Transform currentMainTransform)
    {
        character.detectionSystem.gameObject.tag = "Npc";
        character.detectionSystem.StopScan();

        if (currentMainTransform != null)
            character.movementController.EnableFollow(currentMainTransform);
        else
            character.movementController.DisableMovement();
    }

    public void BuildRestingCharacter(PlayableCharacter character)
    {
        character.detectionSystem.gameObject.tag = "Npc";
        character.detectionSystem.StopScan();

        // Cada personagem conhece seu próprio ponto de descanso
        if (character.restingPoint != null)
            character.movementController.EnableWalkToPoint(character.restingPoint.position);
        else
            character.movementController.DisableMovement();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void ForceExitAllReceivers()
    {
        var receivers = Object.FindObjectsByType<PlayableDetectionReceiver>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (var receiver in receivers)
            receiver.ForceExit();
    }
}