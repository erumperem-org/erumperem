using UnityEngine;

/// <summary>
/// Tuning da IA perseguidora (<see cref="ChaserAI"/>), separado do
/// <c>MovementSettings</c> do serviço de física porque aqui os parâmetros
/// são específicos de comportamento de IA (percepção, perseguição,
/// investigação), não de física de movimentação genérica reutilizável
/// entre qualquer personagem/controlador.
/// </summary>
[CreateAssetMenu(fileName = "ChaserSettings", menuName = "Movement/AI/Chaser Settings")]
public class ChaserSettings : ScriptableObject
{
    [Header("Percepção (raio 360° + linha de visão)")]
    [Tooltip("Distância máxima na qual o alvo pode ser percebido.")]
    public float perceptionRadius = 10f;

    [Tooltip("Altura, a partir da base do perseguidor, de onde parte o raycast de linha de visão.")]
    public float eyeHeight = 1.6f;

    [Tooltip("Intervalo, em segundos, entre cada checagem de percepção. " +
             "Não precisa ser todo frame - raycasts têm custo.")]
    public float perceptionCheckInterval = 0.1f;

    [Tooltip("Camadas que bloqueiam a linha de visão até o alvo (ex: paredes/ambiente).")]
    public LayerMask obstacleMask;

    [Header("Perseguição")]
    [Tooltip("Distância na qual o alvo é considerado 'alcançado'.")]
    public float catchDistance = 1.2f;

    [Tooltip("Se true, ativa sprint no PhysicsMovementService durante a perseguição.")]
    public bool sprintWhileChasing = true;

    [Header("Investigação (após perder o alvo)")]
    [Tooltip("Tempo, em segundos, que o perseguidor espera na última posição vista " +
             "antes de desistir e voltar a vagar.")]
    public float investigateWaitTime = 3f;

    [Tooltip("Distância a partir da qual um destino (waypoint ou última posição vista) " +
             "é considerado 'alcançado'.")]
    public float arrivalThreshold = 0.5f;

    [Header("Resting")]
    [Tooltip("Distância mínima do target que o perseguidor deve atingir antes de ficar " +
             "parado (sem rotina) em Resting. Enquanto a distância atual for menor que " +
             "este valor, ele se afasta do target; ao atingir ou ultrapassar, para de vez.")]
    public float restDepartureDistance = 5f;

    [Header("Desvio de obstáculo (ObstacleAvoidanceValidator)")]
    [Tooltip("Ângulo máximo, para cada lado, testado na busca por um caminho alternativo " +
             "quando a direção desejada está bloqueada.")]
    public float maxAvoidanceAngle = 90f;

    [Tooltip("Incremento angular usado a cada tentativa de encontrar uma direção livre.")]
    public float avoidanceAngleStep = 15f;

    [Header("Desvio de área segura (SafeAreaAvoidanceValidator)")]
    [Tooltip("Distância extra somada ao raio da SafeArea/Hub excluída, para o Chaser nunca chegar bem na borda dela.")]
    public float safeAreaAvoidanceMargin = 2f;

    [Tooltip("Ângulo máximo, para cada lado, testado na busca por um caminho alternativo ao redor da área segura.")]
    public float safeAreaAvoidanceMaxSearchAngle = 150f;
}