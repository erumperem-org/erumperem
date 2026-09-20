using UnityEngine;

public class PlayableCharacterAnimation : MonoBehaviour
{
    private const string LinearVelocityParameter = "LinearVelocity";
    private const string TorchStateParameter = "TorchState";

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Character State")]
    [SerializeField] private CharacterStateExposed characterState;

    private void OnEnable()
    {
        if (characterState == null)
        {
            return;
        }

        characterState.OnMovementStateChanged += OnMovementStateChanged;
        characterState.OnTorchStateChanged += OnTorchStateChanged;

        UpdateMovementAnimation(characterState.MovementState);
        UpdateTorchAnimation(characterState.TorchState);
    }

    private void OnDisable()
    {
        if (characterState == null)
        {
            return;
        }

        characterState.OnMovementStateChanged -= OnMovementStateChanged;
        characterState.OnTorchStateChanged -= OnTorchStateChanged;
    }

    private void OnMovementStateChanged(
        CharacterStateExposed.CharacterMovementState state)
    {
        UpdateMovementAnimation(state);
    }

    private void OnTorchStateChanged(
        CharacterStateExposed.CharacterTorchState state)
    {
        UpdateTorchAnimation(state);
    }

    private void UpdateMovementAnimation(
        CharacterStateExposed.CharacterMovementState state)
    {
        if (animator == null)
        {
            return;
        }

        float linearVelocity = state switch
        {
            CharacterStateExposed.CharacterMovementState.Idle => 0f,
            CharacterStateExposed.CharacterMovementState.Walk => 1f,
            CharacterStateExposed.CharacterMovementState.Run => 2f,
            _ => 0f
        };

        animator.SetFloat(
            LinearVelocityParameter,
            linearVelocity
        );
    }

    private void UpdateTorchAnimation(
        CharacterStateExposed.CharacterTorchState state)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetBool(
            TorchStateParameter,
            state == CharacterStateExposed.CharacterTorchState.On
        );
    }
}