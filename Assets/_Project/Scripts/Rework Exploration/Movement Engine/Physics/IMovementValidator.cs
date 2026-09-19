using UnityEngine;

/// <summary>
/// Estratégia de validação de movimento.
///
/// O <see cref="PhysicsMovementService"/> chama <see cref="Validate"/> a cada
/// FixedUpdate, antes de transformar a direção desejada em velocidade.
/// A implementação decide como corrigir essa direção quando existe um
/// obstáculo à frente — por exemplo, deslizando ao longo de uma parede ou
/// zerando o movimento por completo.
///
/// Essa camada é totalmente opcional: se nenhuma implementação for atribuída
/// ao serviço, o comportamento de movimentação continua o mesmo de antes
/// (a física do Rigidbody ainda impede atravessar colisores, mas o serviço
/// não corrige a direção de input, podendo gerar "empurrão" contínuo contra
/// obstáculos).
/// </summary>
public interface IMovementValidator
{
    /// <summary>
    /// Retorna a direção de movimento corrigida, em world-space.
    /// A magnitude do vetor retornado deve representar a intensidade do
    /// movimento (0 a 1), da mesma forma que <paramref name="desiredDirection"/>.
    /// </summary>
    /// <param name="desiredDirection">Direção desejada, em world-space, com magnitude 0–1.</param>
    /// <param name="origin">Ponto de origem do cast (normalmente o centro do personagem).</param>
    /// <param name="castRadius">Raio usado no SphereCast de verificação.</param>
    /// <param name="checkDistance">Distância à frente a ser verificada.</param>
    /// <param name="obstacleMask">Camadas consideradas obstáculo para este cast.</param>
    Vector3 Validate(Vector3 desiredDirection, Vector3 origin, float castRadius, float checkDistance, LayerMask obstacleMask);
}
