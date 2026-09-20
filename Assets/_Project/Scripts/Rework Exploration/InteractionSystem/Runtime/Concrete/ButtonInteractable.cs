using System.Collections;
using UnityEngine;

namespace InteractionSystem.Concrete
{
    /// <summary>
    /// Interactable simples: ao ser acionado, dispara onInteractionStarted
    /// (herdado de InteractableBase - arraste qualquer sistema externo e
    /// método no Inspector) e se reativa sozinho, imediatamente ou após um
    /// delay configurável. Não depende de nenhum sensor externo.
    /// </summary>
    public class ButtonInteractable : InteractableBase
    {
        [Header("Reativação")]
        [Tooltip("Segundos até o botão voltar a ficar disponível. 0 = imediato.")]
        [SerializeField] private float reactivateDelay = 0f;

        protected override void OnInteractionStarted(IInteractionContext context)
        {
            base.OnInteractionStarted(context); // dispara o UnityEvent onInteractionStarted

            if (reactivateDelay <= 0f)
            {
                SetAvailable(true);
            }
            else
            {
                StartCoroutine(ReactivateAfterDelay());
            }
        }

        private IEnumerator ReactivateAfterDelay()
        {
            yield return new WaitForSeconds(reactivateDelay);
            SetAvailable(true);
        }
    }
}
