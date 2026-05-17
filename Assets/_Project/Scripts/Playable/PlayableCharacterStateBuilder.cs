using System;
using UnityEngine;
using Player;

[Serializable]
public class PlayableCharacterStateBuilder
{
    public PlayerInputReader inputReader;

    public void BuildMain(PlayableCharacter character)
    {
        character.movementController.EnableMovement();
        inputReader.playerDetectionSystem = character.detectionSystem;
        character.gameObject.tag = "Player";
        character.CurrentState = PlayableCharacterState.Main;
        character.detectionSystem.Scan();
    }

    public void BuildCompanion(PlayableCharacter character)
    {
        character.gameObject.tag = "Npc";
        character.movementController.DisableMovement();
        character.CurrentState = PlayableCharacterState.Companion;
        character.detectionSystem.StopScan();
    }

    public void BuildResting(PlayableCharacter character)
    {
        character.gameObject.tag = "Npc";
        character.movementController.DisableMovement();
        character.CurrentState = PlayableCharacterState.Resting;
        character.detectionSystem.StopScan();
    }
}
