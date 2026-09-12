using UnityEngine;

/// <summary>
/// Exemplo de um segundo tipo de controlador, para provar o desacoplamento:
/// este não lê teclado nenhum, apenas anda em direção a um Transform alvo.
/// O PhysicsMovementService não precisa de nenhuma alteração para suportar
/// isso — só de alguém chamando SetMoveDirection.
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
public class SimpleAIMovementController : MonoBehaviour
{
    [SerializeField] private PhysicsMovementService movement;
    [SerializeField] private Transform target;
    [SerializeField] private float stopDistance = 0.5f;
    [SerializeField] private bool sprint = false;

    private void Awake()
    {
        if (movement == null)
            movement = GetComponent<PhysicsMovementService>();
    }

    private void Update()
    {
        if (target == null)
        {
            movement.SetMoveDirection(Vector3.zero);
            return;
        }

        Vector3 toTarget = target.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.magnitude > stopDistance)
            movement.SetMoveDirection(toTarget.normalized);
        else
            movement.SetMoveDirection(Vector3.zero);

        movement.SetSprinting(sprint);
    }
}
