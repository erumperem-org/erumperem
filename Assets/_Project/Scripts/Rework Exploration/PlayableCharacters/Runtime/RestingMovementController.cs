using System;
using UnityEngine;

/// <summary>
/// Controlador do papel "Resting": caminha até o ponto de descanso próprio
/// do personagem (igual ao Investigating do ChaserAI indo até a última
/// posição vista). Ao chegar, desliga a própria rotina (enabled = false) e
/// dispara o callback de chegada - nenhuma checagem roda mais até a
/// próxima vez que este componente for reativado.
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
public class RestingMovementController : MonoBehaviour
{
    private PhysicsMovementService movement;
    private Transform restingPoint;
    private PlayableCharacterSettings settings;
    private Action onArrived;
    private bool hasArrived;

    /// <summary>Chamado uma vez pelo PlayableCharacters no Awake.</summary>
    public void Initialize(Transform destinationPoint, PlayableCharacterSettings characterSettings, Action onArrivedCallback)
    {
        movement = GetComponent<PhysicsMovementService>();
        restingPoint = destinationPoint;
        settings = characterSettings;
        onArrived = onArrivedCallback;
    }

    /// <summary>Chamado pelo PlayableCharacters ao entrar em Resting, antes deste componente ser ativado - reseta o estado de "já chegou" de uma entrada anterior.</summary>
    public void BeginResting()
    {
        hasArrived = false;
    }

    private void OnDisable()
    {
        if (movement == null)
        {
            return;
        }
        movement.SetMoveDirection(Vector3.zero);
        movement.SetSprinting(false);
    }

    private void FixedUpdate()
    {
        if (hasArrived || restingPoint == null)
        {
            return;
        }

        Vector3 flatDelta = restingPoint.position - transform.position;
        flatDelta.y = 0f;

        if (flatDelta.sqrMagnitude <= settings.restingArrivalThreshold * settings.restingArrivalThreshold)
        {
            hasArrived = true;
            movement.SetMoveDirection(Vector3.zero);
            enabled = false; // desliga a própria rotina ao chegar
            onArrived?.Invoke();
            return;
        }

        movement.SetMoveDirection(flatDelta.normalized);
    }
}
