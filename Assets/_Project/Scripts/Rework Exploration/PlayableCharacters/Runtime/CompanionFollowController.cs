using UnityEngine;

/// <summary>
/// Controlador do papel "Companheiro": persegue o personagem Em Jogo
/// eternamente (sem percepção - nunca "perde" o alvo), desviando de
/// obstáculos no caminho via ObstacleAvoidanceValidator (mesmo validador do
/// pacote ChaserAI). Para perto do líder e acelera (sprint) quando fica
/// muito para trás, usando histerese entre duas distâncias para não ficar
/// entrando e saindo de sprint na borda de um único limiar.
///
/// Muito perto do líder (abaixo de pathClearDistance), em vez de apenas
/// parar - o que deixaria o companheiro parado bem na frente/no caminho do
/// líder - ele se desloca lateralmente para o lado em que já está,
/// perpendicular à direção que o líder está encarando, para abrir passagem.
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
public class CompanionFollowController : MonoBehaviour
{
    private PhysicsMovementService movement;
    private PlayableCharacterSettings settings;
    private Transform followTarget;
    private bool isSprinting;

    /// <summary>Chamado uma vez pelo PlayableCharacters no Awake, com as configurações compartilhadas.</summary>
    public void Initialize(PlayableCharacterSettings characterSettings)
    {
        movement = GetComponent<PhysicsMovementService>();
        settings = characterSettings;
        movement.SetValidator(new ObstacleAvoidanceValidator(settings.maxAvoidanceAngle, settings.avoidanceAngleStep));
    }

    /// <summary>Define quem este companheiro deve seguir. Deve ser atualizado sempre que o personagem Em Jogo mudar.</summary>
    public void SetFollowTarget(Transform target)
    {
        followTarget = target;
    }

    private void OnEnable()
    {
        isSprinting = false;
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
        if (followTarget == null)
        {
            movement.SetMoveDirection(Vector3.zero);
            return;
        }

        float distance = Vector3.Distance(transform.position, followTarget.position);

        if (!isSprinting && distance >= settings.catchUpTriggerDistance)
        {
            isSprinting = true;
        }
        else if (isSprinting && distance <= settings.catchUpRecoverDistance)
        {
            isSprinting = false;
        }

        movement.SetSprinting(isSprinting);

        if (distance <= settings.pathClearDistance)
        {
            movement.SetMoveDirection(ComputeSidestepDirection());
            return;
        }

        if (distance <= settings.stopDistance)
        {
            movement.SetMoveDirection(Vector3.zero);
            return;
        }

        movement.SetMoveDirection(DirectionTo(followTarget.position));
    }

    private Vector3 DirectionTo(Vector3 worldPosition)
    {
        Vector3 flatDelta = worldPosition - transform.position;
        flatDelta.y = 0f;
        return flatDelta.sqrMagnitude > 0.0001f ? flatDelta.normalized : Vector3.zero;
    }

    /// <summary>
    /// Calcula uma direção lateral, perpendicular ao forward do líder, para
    /// o lado em que o companheiro já está (evita alternar de lado a cada
    /// frame). Fallback para o lado direito se o companheiro estiver quase
    /// exatamente alinhado com o eixo forward do líder (produto escalar
    /// perto de zero, lado ambíguo).
    /// </summary>
    private Vector3 ComputeSidestepDirection()
    {
        Vector3 leaderForward = followTarget.forward;
        leaderForward.y = 0f;

        if (leaderForward.sqrMagnitude < 0.0001f)
        {
            leaderForward = Vector3.forward;
        }
        else
        {
            leaderForward.Normalize();
        }

        Vector3 sideAxis = Vector3.Cross(Vector3.up, leaderForward); // "direita" do líder

        Vector3 toCompanion = transform.position - followTarget.position;
        toCompanion.y = 0f;

        float side = Vector3.Dot(toCompanion, sideAxis);
        float sideSign = Mathf.Abs(side) > 0.01f ? Mathf.Sign(side) : 1f;

        return sideAxis * sideSign;
    }
}