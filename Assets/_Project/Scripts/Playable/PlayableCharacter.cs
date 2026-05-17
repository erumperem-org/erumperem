using UnityEngine;
using Player;

[RequireComponent(typeof(PlayerMovementController))]
public class PlayableCharacter : MonoBehaviour
{
    public string characterName;
    public PlayableCharacterState CurrentState;
    public PlayerInputReader playerInput;
    public PlayerMovementController movementController;
    public PlayerDetectionSystem detectionSystem;

    private void Awake()
    {
        movementController._inputReader = playerInput;
    }
}
