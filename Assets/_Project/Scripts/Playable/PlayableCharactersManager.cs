using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;

public class PlayableCharactersManager : MonoBehaviour
{
    [SerializeField] private List<PlayableCharacter> playables;
    [SerializeField] private PlayableCharacterStatesBuilder stateBuilder = new();

    public PlayableCharacter MainCharacter;
    public PlayableCharacter CompanionCharacter;
    public event Action<PlayableCharacter> MainCharacterChange;
    public event Action<PlayableCharacter> CompanionCharacterChange;


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
                    stateBuilder.BuildRestingCharacter(character);
                    character.CurrentState = PlayableCharacterState.Resting;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }

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

    // PlayableCharactersManager — PromoteToMain (Caso 2: swap)
    private void PromoteToMain(PlayableCharacter character)
    {
        if (character == CompanionCharacter)
        {
            if (MainCharacter != null)
            {
                stateBuilder.BuildCompanionCharacter(MainCharacter, character.transform);
                MainCharacter.CurrentState = PlayableCharacterState.Companion;
                CompanionCharacter = MainCharacter;
                CompanionCharacterChange?.Invoke(CompanionCharacter);
            }
            else
            {
                CompanionCharacter = null;
                CompanionCharacterChange?.Invoke(null);
            }
        }
        else if (MainCharacter != null && MainCharacter != character)
        {
            SetState(PlayableCharacterState.Resting, MainCharacter);
        }

        stateBuilder.BuildMainCharacter(character);
        character.CurrentState = PlayableCharacterState.Main;
        character.detectionSystem.availableInteractables.Clear();
        MainCharacter = character;
        MainCharacterChange?.Invoke(MainCharacter);

        // Reatualiza o companion com o transform do novo main
        // (cobre o caso: resting → main quando companion já existe)
        if (CompanionCharacter != null && character != CompanionCharacter)
            stateBuilder.BuildCompanionCharacter(CompanionCharacter, MainCharacter.transform);
    }

    // PlayableCharactersManager — PromoteToCompanion (Bug 3: invoke único no fim)
    private void PromoteToCompanion(PlayableCharacter character)
    {
        if (character == MainCharacter)
        {
            MainCharacter = null;
            MainCharacterChange?.Invoke(null);
        }

        if (CompanionCharacter != null && CompanionCharacter != character)
        {
            SetState(PlayableCharacterState.Resting, CompanionCharacter);
        }

        // mainTransform já foi atualizado por BuildMainCharacter — usa direto
        stateBuilder.BuildCompanionCharacter(character, stateBuilder.mainTransform);
        character.CurrentState = PlayableCharacterState.Companion;
        CompanionCharacter = character;
        CompanionCharacterChange?.Invoke(CompanionCharacter); // único invoke, no fim
    }
}