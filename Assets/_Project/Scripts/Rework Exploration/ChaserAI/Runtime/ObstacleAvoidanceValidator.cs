using UnityEngine;

/// <summary>
/// Implementação de <c>IMovementValidator</c> (do projeto base de
/// movimentação por física) voltada para IA: em vez de só deslizar
/// (<c>WallSlideValidator</c>) ou parar seco (<c>HardStopValidator</c>) ao
/// encontrar um obstáculo, tenta ativamente achar outro caminho para o
/// mesmo objetivo, testando direções em ângulos crescentes (alternando para
/// a direita e para a esquerda da direção original) até encontrar uma
/// livre.
///
/// Antes de testar direções, trata separadamente o caso em que a esfera de
/// origem já está sobreposta a um obstáculo (encostado numa parede, por
/// exemplo): nesse caso, um SphereCast normal retorna "bloqueado" com
/// distância ~0 para QUALQUER direção testada, o que faria a busca angular
/// falhar por completo e travar o personagem. Em vez disso, calculamos um
/// vetor de "empurrão para fora" via Physics.ComputePenetration e o usamos
/// como direção prioritária.
///
/// Se, mesmo sem sobreposição, nenhuma direção testada na busca angular
/// estiver livre, cai em um fallback equivalente ao <c>WallSlideValidator</c>
/// (projeta no plano da normal do obstáculo), para nunca travar
/// completamente o personagem.
/// </summary>
[System.Serializable]
public class ObstacleAvoidanceValidator : IMovementValidator
{
    private readonly float maxSearchAngle;
    private readonly float angleStep;

    // Collider auxiliar usado apenas como "sonda" geométrica para
    // Physics.ComputePenetration. Nunca participa da simulação física
    // normal (isTrigger = true, GameObject oculto e fora da hierarquia
    // salva na cena). Compartilhado entre todas as instâncias do
    // validador e criado sob demanda, na primeira vez que for preciso.
    private static SphereCollider probeCollider;

    /// <param name="maxSearchAngle">Ângulo máximo, para cada lado, testado na busca por caminho livre.</param>
    /// <param name="angleStep">Incremento angular entre cada tentativa.</param>
    public ObstacleAvoidanceValidator(float maxSearchAngle = 90f, float angleStep = 15f)
    {
        this.maxSearchAngle = Mathf.Max(0f, maxSearchAngle);
        this.angleStep = Mathf.Max(1f, angleStep);
    }

    public Vector3 Validate(Vector3 desiredDirection, Vector3 origin,
                             float castRadius, float checkDistance, LayerMask obstacleMask)
    {
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            return desiredDirection;
        }

        Vector3 normalizedDirection = desiredDirection.normalized;
        float magnitude = desiredDirection.magnitude;

        // 1) Caso especial: origem já sobreposta a um obstáculo.
        //
        // Isso normalmente acontece quando o personagem chega bem perto de
        // uma parede/cenário e castRadius passa a cobrir uma região que já
        // intersecta o collider. Um SphereCast comum, nesse regime, retorna
        // hit com distância ~0 independente da direção testada — o que
        // contaminaria TODAS as tentativas do loop angular abaixo como
        // "bloqueadas", mesmo as que na prática levariam para longe do
        // obstáculo. Por isso tratamos esse caso antes, com um vetor de
        // empurrão explícito.
        Vector3 pushOut = ComputePushOut(origin, castRadius, obstacleMask);
        if (pushOut.sqrMagnitude > 0.0001f)
        {
            pushOut.y = 0f;

            if (pushOut.sqrMagnitude > 0.0001f)
            {
                return pushOut.normalized * magnitude;
            }
        }

        // 2) Sem sobreposição: segue a lógica original de busca angular.
        if (!IsBlocked(origin, normalizedDirection, castRadius, checkDistance, obstacleMask))
        {
            return desiredDirection;
        }

        for (float angle = angleStep; angle <= maxSearchAngle; angle += angleStep)
        {
            Vector3 rightCandidate = Quaternion.AngleAxis(angle, Vector3.up) * normalizedDirection;
            if (!IsBlocked(origin, rightCandidate, castRadius, checkDistance, obstacleMask))
            {
                return rightCandidate * magnitude;
            }

            Vector3 leftCandidate = Quaternion.AngleAxis(-angle, Vector3.up) * normalizedDirection;
            if (!IsBlocked(origin, leftCandidate, castRadius, checkDistance, obstacleMask))
            {
                return leftCandidate * magnitude;
            }
        }

        // 3) Fallback final: nenhuma direção testada ficou livre.
        if (Physics.SphereCast(origin, castRadius, normalizedDirection, out RaycastHit hit, checkDistance, obstacleMask))
        {
            return Vector3.ProjectOnPlane(desiredDirection, hit.normal);
        }

        return desiredDirection;
    }

    private static bool IsBlocked(Vector3 origin, Vector3 direction, float castRadius,
                                   float checkDistance, LayerMask obstacleMask)
    {
        return Physics.SphereCast(origin, castRadius, direction, out _, checkDistance, obstacleMask);
    }

    /// <summary>
    /// Calcula um vetor de "empurrão para fora" a partir de qualquer
    /// obstáculo que já esteja sobrepondo a esfera de origem. Retorna
    /// Vector3.zero se não houver sobreposição.
    ///
    /// Quando múltiplos colliders se sobrepõem simultaneamente (ex.: um
    /// canto formado por duas paredes), os vetores de penetração de cada
    /// um são somados, o que tende a produzir uma direção resultante
    /// apontando para a "abertura" do canto.
    /// </summary>
    private static Vector3 ComputePushOut(Vector3 origin, float castRadius, LayerMask obstacleMask)
    {
        Collider[] overlaps = Physics.OverlapSphere(origin, castRadius, obstacleMask);
        if (overlaps.Length == 0)
        {
            return Vector3.zero;
        }

        SphereCollider probe = GetProbeCollider(castRadius);
        Vector3 totalPush = Vector3.zero;

        foreach (var col in overlaps)
        {
            // Colliders do tipo Terrain ou outros que não suportam
            // ComputePenetration lançam exceção; ignoramos esses casos
            // com segurança em vez de deixar o validador quebrar.
            bool success;
            Vector3 pushDir;
            float pushDist;

            try
            {
                success = Physics.ComputePenetration(
                    probe, origin, Quaternion.identity,
                    col, col.transform.position, col.transform.rotation,
                    out pushDir, out pushDist);
            }
            catch (System.Exception)
            {
                continue;
            }

            if (success)
            {
                totalPush += pushDir * pushDist;
            }
        }

        return totalPush;
    }

    /// <summary>
    /// Cria (uma única vez, de forma lazy) o collider auxiliar usado como
    /// sonda geométrica. O GameObject fica oculto (HideFlags.HideAndDontSave)
    /// e o collider é marcado como trigger, então nunca aparece na
    /// Hierarchy, nunca é salvo na cena e nunca gera colisão física real —
    /// serve apenas como parâmetro de forma/posição para ComputePenetration.
    /// </summary>
    private static SphereCollider GetProbeCollider(float radius)
    {
        if (probeCollider == null)
        {
            var go = new GameObject("~ObstacleAvoidanceProbe");
            go.hideFlags = HideFlags.HideAndDontSave;
            probeCollider = go.AddComponent<SphereCollider>();
            probeCollider.isTrigger = true;
        }

        probeCollider.radius = radius;
        return probeCollider;
    }
}