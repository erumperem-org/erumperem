using UnityEngine;
using Core.Inventory;

public class GiveUpExplorationRework : MonoBehaviour
{
    public PlayableCharacterController CharacterController;
    public InventoryManager InventoryManager;
    public void OnClick()
    {
        ReturnPartyToVillage();
        ExcludeInventory();
    }
    private void ReturnPartyToVillage()
    {
        if (CharacterController == null)
        {
            Debug.LogError("[GiveUpExplorationRework] Referência de CharacterController não atribuída.", this);
            return;
        }

        var mainCharacter = CharacterController.InGameCharacter;
        if (mainCharacter == null || mainCharacter.RestingPoint == null)
        {
            Debug.LogError("[GiveUpExplorationRework] Referência de MainCharacter ou RestingPoint não atribuída.", this);
            return;
        }

        var companionCharacter = CharacterController.CompanionCharacter;
        if (companionCharacter == null || companionCharacter.RestingPoint == null)
        {
            Debug.LogError("[GiveUpExplorationRework] Referência de CompanionCharacter ou RestingPoint não atribuída.", this);
            return;
        }

        mainCharacter.transform.position = mainCharacter.RestingPoint.position;
        companionCharacter.transform.position = companionCharacter.RestingPoint.position;

        CharacterController.SaveAsync();
    }

    private void ExcludeInventory()
    {
        InventoryManager.ClearInventory(InventoryType.Losable);
    }
}
