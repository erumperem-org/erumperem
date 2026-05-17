using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CharacterViewHud : MonoBehaviour
{
    [Header("Referências")]
    public PlayableCharactersManager manager;
    public Image displayIcon;
    public TextMeshProUGUI displayName;

    [SerializeField] private CharacterType typeWanted;

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        switch (typeWanted)
        {
            case CharacterType.Main:
                manager.MainCharacterChange += UpdateView;
                break;
            case CharacterType.Companion:
                manager.CompanionCharacterChange += UpdateView;
                break;
        }
    }

    private void Start()
    {
        UpdateView(GetCurrentCharacter());
    }

    private void OnDestroy()
    {
        switch (typeWanted)
        {
            case CharacterType.Main:
                manager.MainCharacterChange -= UpdateView;
                break;
            case CharacterType.Companion:
                manager.CompanionCharacterChange -= UpdateView;
                break;
        }
    }

    // ── View ───────────────────────────────────────────────────────────────

    private void UpdateView(PlayableCharacter character)
    {
        bool hasCharacter = character != null;
        gameObject.SetActive(hasCharacter);

        if (!hasCharacter) return;

        displayIcon.sprite = character.icon;
        displayName.text   = character.characterName;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private PlayableCharacter GetCurrentCharacter() => typeWanted switch
    {
        CharacterType.Main      => manager.MainCharacter,
        CharacterType.Companion => manager.CompanionCharacter,
        _                       => null
    };

    private enum CharacterType { Main, Companion }
}