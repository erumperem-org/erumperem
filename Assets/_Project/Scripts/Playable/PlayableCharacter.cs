using Player;
using UnityEngine;

[RequireComponent(typeof(PlayerMovementController))]
public class PlayableCharacter : MonoBehaviour
{
    [Header("Identificação")]
    public string characterName;
    public Sprite icon;

    [Header("Estado")]
    public PlayableCharacterState CurrentState;

    [Header("Sub-systems")]
    public PlayerInputReader playerInput;
    public PlayerMovementController movementController;
    public PlayerDetectionSystem detectionSystem;

    [Header("Resting")]
    [Tooltip("Posição para onde este personagem caminha ao entrar em Resting.")]
    public Transform restingPoint;

    private void Awake()
    {
        movementController._inputReader = playerInput;
    }
}