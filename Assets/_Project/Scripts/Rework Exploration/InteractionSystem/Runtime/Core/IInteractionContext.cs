using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Contrato mínimo de um contexto de interação.
    /// </summary>
    public interface IInteractionContext
    {
        /// <summary>Quem foi interagido.</summary>
        IInteractable Interactable { get; }

        /// <summary>Quem iniciou a interação (o instigador atual).</summary>
        GameObject Instigator { get; }
    }
}
