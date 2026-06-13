using UnityEngine;

[System.Serializable]
public sealed class PlayableCharacterStatesBuilder
{
    [Header("Input (Main)")]
    public Player.PlayerInputReader inputReader;

    [Header("Companion")]
    [Tooltip("Referência ao transform do Main atual — atualizada em BuildMainCharacter.")]
    public Transform mainTransform;

    // ── Build states ──────────────────────────────────────────────────────

    public void BuildMainCharacter(PlayableCharacter character)
    {
        ForceExitAllReceivers();

        mainTransform = character.Transform;

        character.MovementController.EnableMovement();
        character.DetectionSystem.SetTag("Player");
        character.DetectionSystem.StartScan();

        inputReader?.BindDetectionSystem(character.DetectionSystem);
    }

    public void BuildCompanionCharacter(PlayableCharacter character, Transform currentMainTransform)
    {
        character.DetectionSystem.SetTag("Npc");
        character.DetectionSystem.StopScan();

        if (currentMainTransform != null)
            character.MovementController.EnableFollow(currentMainTransform);
        else
            character.MovementController.DisableMovement();
    }

    public void BuildRestingCharacter(PlayableCharacter character)
    {
        character.DetectionSystem.SetTag("Npc");
        character.DetectionSystem.StopScan();

        if (character.RestingPoint != null)
            character.MovementController.EnableWalkToPoint(character.RestingPoint.position);
        else
            character.MovementController.DisableMovement();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ForceExitAllReceivers()
    {
        var receivers = Object.FindObjectsByType<PlayableDetectionReceiver>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (var receiver in receivers)
            receiver.ForceExit();
    }
}