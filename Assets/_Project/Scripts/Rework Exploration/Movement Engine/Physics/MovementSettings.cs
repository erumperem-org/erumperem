using UnityEngine;

/// <summary>
/// Conjunto de parâmetros de tuning para o <see cref="PhysicsMovementService"/>.
/// Guardar isso em um ScriptableObject permite reutilizar o mesmo perfil de
/// movimentação entre vários personagens/prefabs (ex: "Settings_Player",
/// "Settings_Zumbi") sem duplicar valores em cada instância de cena.
/// </summary>
[CreateAssetMenu(fileName = "MovementSettings", menuName = "Movement/Physics Movement Settings")]
public class MovementSettings : ScriptableObject
{
    [Header("Velocidade")]
    [Tooltip("Velocidade horizontal alvo ao andar.")]
    public float walkSpeed = 4f;

    [Tooltip("Velocidade horizontal alvo ao correr (SetSprinting(true)).")]
    public float sprintSpeed = 7f;

    [Header("Aceleração")]
    [Tooltip("Taxa (unidades/s²) de aproximação da velocidade atual até a velocidade alvo, quando há input.")]
    public float acceleration = 40f;

    [Tooltip("Taxa (unidades/s²) de aproximação da velocidade atual até zero, quando não há input.")]
    public float deceleration = 50f;

    [Header("Rotação")]
    [Tooltip("Se verdadeiro, rotaciona o Rigidbody suavemente na direção do movimento.")]
    public bool rotateTowardsMovement = true;

    [Tooltip("Velocidade de interpolação da rotação (maior = gira mais rápido).")]
    public float rotationSpeed = 12f;

    [Header("Detecção de chão")]
    [Tooltip("Raio do SphereCast usado para checar se o personagem está no chão.")]
    public float groundCheckRadius = 0.3f;

    [Tooltip("Distância abaixo do personagem verificada pelo SphereCast de chão.")]
    public float groundCheckDistance = 0.35f;

    [Tooltip("Camadas consideradas 'chão'.")]
    public LayerMask groundMask = ~0;

    [Header("Validação de obstáculos (opcional)")]
    [Tooltip("Altura, relativa à base do personagem, de onde o cast de obstáculo parte (evita colidir com o próprio chão).")]
    public float obstacleCastHeight = 0.5f;

    [Tooltip("Raio do SphereCast usado para detectar obstáculos à frente.")]
    public float obstacleCastRadius = 0.3f;

    [Tooltip("Distância à frente verificada pelo validador de movimento.")]
    public float obstacleCheckDistance = 0.4f;

    [Tooltip("Camadas consideradas obstáculo para a validação de movimento.")]
    public LayerMask obstacleMask = ~0;
}
