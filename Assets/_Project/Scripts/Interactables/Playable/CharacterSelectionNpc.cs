using UnityEngine;
using Player;
public sealed class CharacterSelectionNpc : Interactable
{
    [Header("Personagem representado por este NPC")]
    [SerializeField] private PlayableCharacter _character;

    [Header("Canvas de seleção")]
    [SerializeField] private CharacterSelectionCanvas _canvas;

    // Sempre interagível — o canvas mostra os botões desabilitados quando necessário
    public override bool CanInteract => true;

    public override void ExecuteInteraction(PlayerMovementController controller)
    {
        if (_character == null || _canvas == null) return;
        _canvas.Open(_character);
    }
}