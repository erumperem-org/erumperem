using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Implementação padrão de IInteractionContext, usada por qualquer
    /// InteractableBase que não precise de dados extras. Pode ser
    /// herdada caso um interactable específico precise carregar payload
    /// próprio (ex: item de um baú) - mas isso não é necessário apenas
    /// para integrar com sistemas externos, já que isso é feito via
    /// UnityEvent (com parâmetros configurados direto no Inspector).
    /// </summary>
    public class InteractionContext : IInteractionContext
    {
        public IInteractable Interactable { get; }
        public GameObject Instigator { get; }

        public InteractionContext(IInteractable interactable, GameObject instigator)
        {
            Interactable = interactable;
            Instigator = instigator;
        }
    }
}
