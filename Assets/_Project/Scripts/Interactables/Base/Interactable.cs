using System.Collections;
using DetectionSystem.Core;
using Player;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    [Tooltip("Duração em segundos até o movimento ser reativado após a animação.")]
    [SerializeField] private float reenableDelay = 9;
    public DetectionReceiver receiver;
    public virtual bool CanInteract { get; }
    public virtual void ExecuteInteraction(PlayerMovementController controller) { }
    public virtual void Init()
    {
        this.receiver = this.gameObject.GetComponent<DetectionReceiver>();
        if (receiver is InteractableDetectionReceiver interactable)
        {
            interactable.interactable = this;
        }
    }
    protected IEnumerator ReenableAfterAnimation(PlayerMovementController controller)
    {
        yield return new WaitForSeconds(reenableDelay);
        Debug.Log("Reativando Input");
        controller._inputReader.IsPlayerInteracting = false;
    }
}
