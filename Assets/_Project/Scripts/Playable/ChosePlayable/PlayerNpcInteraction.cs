using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vincula cada PlayableCharacter ao seu canvas de escolha.
/// Para adicionar novos personagens, basta inserir uma entrada no array pelo Inspector.
/// </summary>
public class PlayerNpcInteraction : MonoBehaviour
{
    [Serializable]
    private struct CharacterEntry
    {
        public PlayableCharacter character;
        public GameObject canvas;
    }

    [SerializeField] private List<CharacterEntry> entries;
    [SerializeField] private PlayableCharactersManager manager;

    public void ChoseAsMain(PlayableCharacter character)
    {
        manager.SetState(PlayableCharacterState.Main, character);
        CloseCanvas(character);
    }

    public void ChoseAsCompanion(PlayableCharacter character)
    {
        manager.SetState(PlayableCharacterState.Companion, character);
        CloseCanvas(character);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void CloseCanvas(PlayableCharacter character)
    {
        CharacterEntry entry = entries.Find(e => e.character == character);
        entry.canvas?.SetActive(false);
    }
}
