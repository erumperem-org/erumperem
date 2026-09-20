using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterStatusShower : MonoBehaviour
{
    public PlayableCharacterController controller;

    public Image icon;
    public TMP_Text displayName, lifeText, speedText, defenseText, criticalText;

    public void RenderInfo(CharacterState playableStateToShow)
    {
        switch (playableStateToShow)
        {
            case CharacterState.InGame:
                icon.sprite = controller.InGameCharacter.info.Icon;
                displayName.text = controller.InGameCharacter.info.DisplayName;
                lifeText.text = controller.InGameCharacter.info.MaxHitPoints.ToString();;
                speedText.text = controller.InGameCharacter.info.Speed.ToString();
                defenseText.text = controller.InGameCharacter.info.DefenseChance.ToString();
                criticalText.text = controller.InGameCharacter.info.CritChance.ToString();
                break;

            case CharacterState.Companion:
                icon.sprite = controller.CompanionCharacter.info.Icon;
                displayName.text = controller.CompanionCharacter.info.DisplayName;
                lifeText.text = controller.CompanionCharacter.info.MaxHitPoints.ToString();
                speedText.text = controller.CompanionCharacter.info.Speed.ToString();
                defenseText.text = controller.CompanionCharacter.info.DefenseChance.ToString();
                criticalText.text = controller.CompanionCharacter.info.CritChance.ToString();
                break;

            case CharacterState.Resting:
                icon.sprite = controller.RestingCharacter.info.Icon;
                displayName.text = controller.RestingCharacter.info.DisplayName;
                lifeText.text = controller.RestingCharacter.info.MaxHitPoints.ToString();
                speedText.text = controller.RestingCharacter.info.Speed.ToString();
                defenseText.text = controller.RestingCharacter.info.DefenseChance.ToString();
                criticalText.text = controller.RestingCharacter.info.CritChance.ToString();
                break;
        }
    }

    public void ShowInGame()
    {
        RenderInfo(CharacterState.InGame);
    }

    public void ShowCompanion()
    {
        RenderInfo(CharacterState.Companion);
    }

    public void ShowResting()
    {
        RenderInfo(CharacterState.Resting);
    }
}