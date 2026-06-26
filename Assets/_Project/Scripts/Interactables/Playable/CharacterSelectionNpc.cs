using UnityEngine;

/// <summary>
/// NPC que abre o canvas de seleção de personagem quando interagido.
/// Não depende de <c>PlayerMovementController</c> — usa <see cref="InteractionContext"/>.
/// </summary>
public sealed class CharacterSelectionNpc : Interactable
{
    [SerializeField] private PlayableCharacter      _character;
    [SerializeField] public CharacterSelectionCanvas _canvas;

    public override bool CanInteract => true;

    public override void ExecuteInteraction(InteractionContext context)
    {
        if (_character == null || _canvas == null) return;
        _canvas.Open(_character);
    }
}
