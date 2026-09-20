using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    [Header("Main")]
    [SerializeField] private GameObject dialoguePanel;

    [Header("Character")]
    [SerializeField] private TMP_Text speakerNameText;
    [SerializeField] private Image portraitImage;

    [Header("Dialogue")]
    [SerializeField] private TMP_Text dialogueText;

    [Header("Continue")]
    [SerializeField] private Button continueButton;

    [Header("Choices")]
    [SerializeField] private Transform choicesContainer;
    [SerializeField] private Button choiceButtonPrefab;

    private void Awake()
    {
        Hide();
    }

    public void ShowNode(DialogueNode node, Action onContinue, Action<DialogueChoice> onChoice)
    {
        dialoguePanel.SetActive(true);

        UpdateSpeaker(node);
        UpdateText(node);

        ClearChoices();

        bool hasChoices =
            node.Choices != null &&
            node.Choices.Count > 0;

        if (hasChoices)
        {
            ShowChoices(node, onChoice);

            continueButton.gameObject.SetActive(false);
        }
        else
        {
            SetupContinueButton(onContinue);
        }
    }

    private void UpdateSpeaker(DialogueNode node)
    {
        DialogueCharacter character = node.Speaker;

        if (character == null)
        {
            speakerNameText.text = "";

            speakerNameText.gameObject.SetActive(false);

            portraitImage.sprite = null;
            portraitImage.gameObject.SetActive(false);

            return;
        }

        speakerNameText.gameObject.SetActive(true);

        speakerNameText.text = character.DisplayName;
        speakerNameText.color = character.NameColor;

        Sprite portrait;

        if (node.PortraitOverride != null)
        {
            portrait = node.PortraitOverride;
        }
        else
        {
            portrait = character.DefaultPortrait;
        }

        portraitImage.sprite = portrait;

        portraitImage.gameObject.SetActive(portrait != null);
    }

    private void UpdateText(DialogueNode node)
    {
        dialogueText.text = node.Text;
    }

    private void SetupContinueButton(Action onContinue)
    {
        continueButton.gameObject.SetActive(true);

        continueButton.onClick.RemoveAllListeners();

        continueButton.onClick.AddListener(() =>
        {
            onContinue?.Invoke();
        });
    }

    private void ShowChoices(DialogueNode node, Action<DialogueChoice> onChoice)
    {
        foreach (DialogueChoice choice in node.Choices)
        {
            DialogueChoice currentChoice = choice;

            Button button = Instantiate(choiceButtonPrefab, choicesContainer);

            TMP_Text buttonText = button.GetComponentInChildren<TMP_Text>();

            if (buttonText != null)
            {
                buttonText.text = choice.Text;
            }

            button.onClick.RemoveAllListeners();

            button.onClick.AddListener(() =>
            {
                onChoice?.Invoke(currentChoice);
            });
        }
    }

    private void ClearChoices()
    {
        if (choicesContainer == null)
            return;

        for (int i = choicesContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(choicesContainer.GetChild(i).gameObject);
        }
    }

    public void Show()
    {
        dialoguePanel.SetActive(true);
    }

    public void Hide()
    {
        ClearChoices();

        dialoguePanel.SetActive(false);
    }
}