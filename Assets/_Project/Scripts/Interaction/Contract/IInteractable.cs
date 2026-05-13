using Services.DebugUtilities.Console;
using Services.DebugUtilities;
using UnityEngine;
using CharacterSystem.Core;
public interface IInteractable
{
    Transform Transform { get; }
    bool CanInteract { get; }
    void Interact();
    InteractionType InteractionType { get; }
}
/// <summary>Contrato para itens que podem ser coletados do chão.</summary>
public interface IPickable
{
    /// <summary>Coleta o item (chamado pela camada Interaction após animação de pickup).</summary>
    void Pickup(PlayerContext context);
}

/// <summary>Tipos de interação — determina qual animação e estado usar.</summary>
public enum InteractionType
{
    OpenDoor,
    Pickup,
    OpenChest
}

