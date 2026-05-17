using System;
using System.Collections.Generic;
using UnityEngine;
using Services.DebugUtilities;

public class PlayableCharactersManager : MonoBehaviour
{
    [SerializeField] private List<PlayableCharacter> playables;
    [SerializeField] private PlayableCharacterStateBuilder stateBuilder = new();

    public PlayableCharacter MainCharacter { get; private set; }
    public PlayableCharacter CompanionCharacter { get; private set; }

    private void Start()
    {
        SetState(PlayableCharacterState.Main, playables[0]);
    }

    public void SetState(PlayableCharacterState newState, PlayableCharacter character)
    {
        if (newState == character.CurrentState) return;

        try
        {
            switch (newState)
            {
                case PlayableCharacterState.Main:
                    PromoteToMain(character);
                    break;

                case PlayableCharacterState.Companion:
                    PromoteToCompanion(character);
                    break;

                case PlayableCharacterState.Resting:
                    stateBuilder.BuildResting(character);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }

            character.CurrentState = newState;

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[{character.characterName.ToUpper()}] Estado alterado para {newState}.",
                LogCategory.Player);
        }
        catch (Exception e)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[{character.characterName.ToUpper()}] Falha ao alterar estado para {newState}: {e}",
                LogCategory.Player);
            throw;
        }
    }

    // ── Helpers privados ──────────────────────────────────────────────────

    private void PromoteToMain(PlayableCharacter character)
    {
        if (character == CompanionCharacter)
        {
            // Swap: companion vira main, main atual vai para companion
            if (MainCharacter != null)
            {
                stateBuilder.BuildCompanion(MainCharacter);
                CompanionCharacter = MainCharacter;
            }
            else
            {
                CompanionCharacter = null;
            }
        }
        else if (MainCharacter != null && MainCharacter != character)
        {
            SetState(PlayableCharacterState.Resting, MainCharacter);
        }

        stateBuilder.BuildMain(character);
        MainCharacter = character;
    }

    private void PromoteToCompanion(PlayableCharacter character)
    {
        if (character == MainCharacter)
            MainCharacter = null;

        if (CompanionCharacter != null && CompanionCharacter != character)
            SetState(PlayableCharacterState.Resting, CompanionCharacter);

        stateBuilder.BuildCompanion(character);
        CompanionCharacter = character;
    }
}
