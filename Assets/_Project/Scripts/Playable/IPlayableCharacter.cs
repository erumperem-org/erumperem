using UnityEngine;

/// <summary>
/// Contrato público de um personagem jogável.
/// Qualquer sistema externo (câmera, UI, combate) deve depender desta interface,
/// nunca de PlayableCharacter diretamente.
/// </summary>
public interface IPlayableCharacter
{
    string CharacterName { get; }
    Sprite Icon           { get; }
    Transform Transform   { get; }
    PlayableCharacterState CurrentState { get; }
}
