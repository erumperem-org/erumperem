using System;
using UnityEngine;

/// <summary>
/// Controlador do papel "Resting": caminha até o ponto de descanso próprio
/// do personagem.
///
/// O estado de movimento é exposto através do CharacterStateExposed:
/// - Walk: enquanto o personagem está se deslocando até o ponto.
/// - Idle: quando chega ao ponto.
///
/// Ao chegar, a própria rotina é desativada.
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
[RequireComponent(typeof(CharacterStateExposed))]
public class RestingMovementController : MonoBehaviour
{
    private PhysicsMovementService movement;
    private CharacterStateExposed characterState;

    private Transform restingPoint;
    private PlayableCharacterSettings settings;
    private Action onArrived;

    private bool hasArrived;

    /// <summary>
    /// Chamado uma vez pelo PlayableCharacters no Awake.
    /// </summary>
    public void Initialize(
        Transform destinationPoint,
        PlayableCharacterSettings characterSettings,
        Action onArrivedCallback)
    {
        movement = GetComponent<PhysicsMovementService>();
        characterState = GetComponent<CharacterStateExposed>();

        restingPoint = destinationPoint;
        settings = characterSettings;
        onArrived = onArrivedCallback;
    }

    /// <summary>
    /// Chamado pelo PlayableCharacters ao entrar em Resting,
    /// antes deste componente ser ativado.
    /// </summary>
    public void BeginResting()
    {
        hasArrived = false;

        if (characterState != null)
        {
            characterState.SetMovementState(
                CharacterStateExposed.CharacterMovementState.Walk
            );
        }
    }

    private void OnDisable()
    {
        if (movement != null)
        {
            movement.SetMoveDirection(Vector3.zero);
            movement.SetSprinting(false);
        }

        if (characterState != null)
        {
            characterState.SetMovementState(
                CharacterStateExposed.CharacterMovementState.Idle
            );
        }
    }

    private void FixedUpdate()
    {
        if (hasArrived || restingPoint == null)
        {
            return;
        }

        Vector3 flatDelta =
            restingPoint.position - transform.position;

        flatDelta.y = 0f;

        float arrivalDistance =
            settings.restingArrivalThreshold;

        if (flatDelta.sqrMagnitude <=
            arrivalDistance * arrivalDistance)
        {
            ArriveAtRestingPoint();
            return;
        }

        movement.SetMoveDirection(
            flatDelta.normalized
        );

        movement.SetSprinting(false);

        characterState.SetMovementState(
            CharacterStateExposed.CharacterMovementState.Walk
        );
    }

    private void ArriveAtRestingPoint()
    {
        hasArrived = true;

        movement.SetMoveDirection(Vector3.zero);
        movement.SetSprinting(false);

        characterState.SetMovementState(
            CharacterStateExposed.CharacterMovementState.Idle
        );

        // Desliga a própria rotina ao chegar.
        enabled = false;

        onArrived?.Invoke();
    }
}