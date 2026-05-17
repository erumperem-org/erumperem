using Player;
using UnityEngine;

public sealed class PlayableCharacterInteractable : Interactable
{
    [SerializeField] private GameObject dialogueCanvas;

    public override bool CanInteract => true;

    public override void ExecuteInteraction(PlayerMovementController controller)
    {
        dialogueCanvas.SetActive(true);
        controller.DisableMovement();
        StartCoroutine(ReenableAfterAnimation(controller));
    }
}
