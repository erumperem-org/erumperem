using UnityEngine;

[System.Serializable]
public sealed class PlayableStateTransitioner
{
    /// <summary>Transform do Main atual. Atualizado sempre que um Main é configurado.</summary>
    public Transform MainTransform { get; private set; }

    // ── API pública ───────────────────────────────────────────────────────

    public void ApplyMain(PlayableCharacter character, Player.PlayerInputReader inputReader)
    {
        ForceExitAllDetectionReceivers();

        MainTransform = character.Transform;

        character.MovementController.SetInputReader(inputReader);
        character.MovementController.EnableMovement();
        SetPhysicsLayerRecursively(character.gameObject, LayerMask.NameToLayer("Player"));
        character.DetectionSystem.SetTag("Player");
        character.DetectionSystem.StartScan();

        inputReader?.BindDetectionSystem(character.DetectionSystem);
    }

    public void ApplyCompanion(PlayableCharacter character)
    {
        SetPhysicsLayerRecursively(character.gameObject, LayerMask.NameToLayer("Default"));
        character.DetectionSystem.SetTag("Npc");
        character.DetectionSystem.StopScan();

        if (MainTransform != null)
            character.MovementController.EnableFollow(MainTransform);
        else
            character.MovementController.DisableMovement();
    }

    public void ApplyResting(PlayableCharacter character)
    {
        SetPhysicsLayerRecursively(character.gameObject, LayerMask.NameToLayer("Default"));
        character.DetectionSystem.SetTag("Npc");
        character.DetectionSystem.StopScan();

        if (character.RestingPoint != null)
            character.MovementController.EnableWalkToPoint(character.RestingPoint.position);
        else
            character.MovementController.DisableMovement();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void ForceExitAllDetectionReceivers()
    {
        var receivers = Object.FindObjectsByType<PlayableDetectionReceiver>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (var r in receivers)
            r.ForceExit();
    }

    private static void SetPhysicsLayerRecursively(GameObject gameObject, int layer)
    {
        if (gameObject == null || layer < 0) return;

        gameObject.layer = layer;
        foreach (Transform child in gameObject.transform)
            SetPhysicsLayerRecursively(child.gameObject, layer);
    }
}