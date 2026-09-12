using UnityEngine;

[CreateAssetMenu(
    fileName = "NewDialogueCharacter",
    menuName = "Dialogue/Character"
)]
public class DialogueCharacter : ScriptableObject
{
    [Header("Identification")]
    [SerializeField] private string characterId;
    [SerializeField] private string displayName;

    [Header("Visual")]
    [SerializeField] private Sprite defaultPortrait;
    [SerializeField] private Color nameColor = Color.white;

    public string CharacterId => characterId;
    public string DisplayName => displayName;
    public Sprite DefaultPortrait => defaultPortrait;
    public Color NameColor => nameColor;
}