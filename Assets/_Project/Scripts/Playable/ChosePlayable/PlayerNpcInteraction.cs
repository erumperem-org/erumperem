using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vincula cada <see cref="PlayableCharacter"/> ao seu canvas de seleção.
/// Responsabilidade única: fechar o canvas e delegar a transição de estado ao Manager.
/// </summary>
public sealed class PlayerNpcInteraction : MonoBehaviour
{
    [Serializable]
    private struct CharacterEntry
    {
        public PlayableCharacter character;
        public GameObject        canvas;
    }

    [SerializeField] private List<CharacterEntry>       _entries;
    [SerializeField] private PlayableCharactersManager  _manager;

    public void ChooseAsMain(PlayableCharacter character)
    {
        _manager.SetState(PlayableCharacterState.Main, character);
        CloseCanvas(character);
    }

    public void ChooseAsCompanion(PlayableCharacter character)
    {
        _manager.SetState(PlayableCharacterState.Companion, character);
        CloseCanvas(character);
    }

    private void CloseCanvas(PlayableCharacter character)
    {
        var entry = _entries.Find(e => e.character == character);
        entry.canvas?.SetActive(false);
    }
}
