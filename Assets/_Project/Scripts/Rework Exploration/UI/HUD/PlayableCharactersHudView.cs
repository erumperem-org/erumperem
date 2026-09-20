using System;
using System.Collections.Generic;
using BarSystem.Bars.Stamina;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class PlayableCharactersHudView : MonoBehaviour
{
    [Serializable]
    public struct PlayableCharactersHudViewElements
    {
        public Image icon;
        public TMP_Text name;
    }

    public PlayableCharactersHudViewElements playableCharactersHudViewElements;
    public PlayableCharacterController playableCharacterController;
    public CharacterState desiredCharacterRole;
    void Awake()
    {
        switch (desiredCharacterRole)
        {
            case CharacterState.InGame:
                playableCharacterController.OnCharacterEnteredInGame += UpdateView;
                break;
            case CharacterState.Companion:
                playableCharacterController.OnCharacterEnteredCompanion += UpdateView;

                break;
            case CharacterState.Resting:
                playableCharacterController.OnCharacterEnteredResting += UpdateView;
                break;
        }
    }

    void OnDestroy()
    {
        switch (desiredCharacterRole)
        {
            case CharacterState.InGame:
                playableCharacterController.OnCharacterEnteredInGame -= UpdateView;
                break;
            case CharacterState.Companion:
                playableCharacterController.OnCharacterEnteredCompanion -= UpdateView;

                break;
            case CharacterState.Resting:
                playableCharacterController.OnCharacterEnteredResting -= UpdateView;
                break;
        }
    }

    void UpdateView(PlayableCharacters playable)
    {
        playableCharactersHudViewElements.icon.sprite = playable.info.Icon;
        playableCharactersHudViewElements.name.text = playable.info.DisplayName;
    }
}
