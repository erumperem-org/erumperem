using System;
using UnityEngine;

namespace InteractionSystem
{
    /// <summary>
    /// Contrato base de qualquer coisa interagível no jogo (NPC, botão, baú, etc.).
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Se a interação pode ser realizada agora.</summary>
        bool CanInteract { get; }

        /// <summary>Disparado sempre que CanInteract muda de valor.</summary>
        event Action<bool> AvailabilityChanged;

        /// <summary>
        /// Tenta realizar a interação. Se CanInteract for false, retorna
        /// false e context sai null. Se true, a interação é executada, o
        /// próprio IInteractable gera (ou recupera) seu contexto e o
        /// devolve via out.
        /// </summary>
        bool TryInteract(GameObject instigator, out IInteractionContext context);

        /// <summary>
        /// Define disponibilidade manualmente. Usado por:
        /// - a própria interação, quando ela sabe reabilitar sozinha (ex: botão);
        /// - qualquer coisa externa - um sensor, ou um evento de outro
        ///   sistema ligado direto no Inspector (ex: "fim de diálogo").
        /// </summary>
        void SetAvailable(bool available);
    }
}
