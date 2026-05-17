using System.Collections;
using Player;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Duração em segundos até o movimento ser reativado após a animação.")]
    [SerializeField] private float reenableDelay = 9;

    public virtual bool CanInteract { get; }
    public virtual void ExecuteInteraction(PlayerMovementController controller) { }
    protected IEnumerator ReenableAfterAnimation(PlayerMovementController controller)
    {
        yield return new WaitForSeconds(reenableDelay);
        Debug.Log("Reativando Input");
        controller._inputReader.IsPlayerInteracting = false;
    }
}
