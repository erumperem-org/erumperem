using Erumperem.Characters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterSelectionPanel : MonoBehaviour
{
    [Header("Referências de UI")]
    public TMP_Text characterName;
    public TMP_Text characterRole;
    public Button promoteToInGameButton;
    public Button promoteToCompanionButton;

    [Header("Controlador central")]
    [SerializeField] private PlayableCharacterController controller;

    private PlayableCharacters selectedCharacter;

    public void Open(PlayableCharacters character)
    {
        selectedCharacter = character;
        gameObject.SetActive(true);
    }

    void OnEnable()
    {
        if (selectedCharacter == null)
        {
            Debug.LogError($"{nameof(CharacterSelectionPanel)} foi ativado sem selectedCharacter definido. Use Open(character) em vez de SetActive diretamente.");
            return;
        }

        RefreshUI();
    }

    private void RefreshUI()
    {
        AllyCharacterStatDefinition selectedCharacterData = selectedCharacter.info;

        characterName.text = selectedCharacterData.DisplayName;
        characterRole.text = selectedCharacter.CurrentState.ToString();

        bool isCompanion = selectedCharacter.CurrentState == CharacterState.Companion;

        // Se for Companion, o botão de "main" faz swap em vez de promote,
        // então continua habilitado (nunca desabilita aqui).
        promoteToInGameButton.interactable = true;
        promoteToCompanionButton.interactable = !isCompanion;

        promoteToInGameButton.onClick.RemoveAllListeners();
        promoteToCompanionButton.onClick.RemoveAllListeners();

        promoteToInGameButton.onClick.AddListener(HandlePromoteToInGame);
        promoteToCompanionButton.onClick.AddListener(HandlePromoteToCompanion);
    }

    private void HandlePromoteToInGame()
    {
        if (selectedCharacter.CurrentState == CharacterState.Companion)
        {
            controller.ExecuteOperation(new SwapInGameAndCompanionOperation());
        }
        else
        {
            controller.ExecuteOperation(new PromoteToInGameOperation(selectedCharacter.CharacterId));
        }

        gameObject.SetActive(false);
    }

    private void HandlePromoteToCompanion()
    {
        controller.ExecuteOperation(new PromoteToCompanionOperation(selectedCharacter.CharacterId));
        gameObject.SetActive(false);
    }
}