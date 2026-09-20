using System;
using Unity.VisualScripting;
using UnityEngine;

public class CharacterStateExposed : MonoBehaviour
{
    public enum CharacterMovementState
    {
        Idle,
        Walk,
        Run
    }

    public enum CharacterInteractionState
    {
        NotInteracting,
        Interacting
    }

    public enum CharacterTorchState
    {
        On,
        Off
    }

    [Header("References")]
    [SerializeField] private TorchManager _torchManager;
    
    private CharacterMovementState _movementState;
    private CharacterInteractionState _interactionState;
    private CharacterTorchState _torchState;

    public CharacterMovementState MovementState => _movementState;
    public CharacterInteractionState InteractionState => _interactionState;
    public CharacterTorchState TorchState => _torchState;

    public event Action<CharacterMovementState> OnMovementStateChanged;
    public event Action<CharacterInteractionState> OnInteractionStateChanged;
    public event Action<CharacterTorchState> OnTorchStateChanged;

    void Start()
    {
        if(_torchManager == null)
        {
            _torchManager = GameObject.FindAnyObjectByType<TorchManager>();
        }
        _torchManager.OnTorchStateChange += HandleTorchState;
    }

    private void HandleTorchState(bool state)
    {
        switch (state)
        {
            case true:
                SetTorchState(CharacterTorchState.On);
                break;
            case false:
                SetTorchState(CharacterTorchState.Off);
                break;
        }
    }
    public void SetMovementState(CharacterMovementState state)
    {
        _movementState = state;
        OnMovementStateChanged?.Invoke(_movementState);
    }

    public void SetInteractionState(CharacterInteractionState state)
    {
        _interactionState = state;
        OnInteractionStateChanged?.Invoke(_interactionState);
    }

    public void SetTorchState(CharacterTorchState state)
    {
        _torchState = state;
        OnTorchStateChanged?.Invoke(_torchState);
    }
}