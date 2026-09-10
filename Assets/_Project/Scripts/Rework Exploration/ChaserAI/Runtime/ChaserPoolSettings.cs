using UnityEngine;

/// <summary>
/// Tuning do ChaserPool: contagem desejada, distâncias de spawn/retorno
/// (com margem de histerese entre elas) e a aproximação simplificada de
/// "campo de visão" usada para não spawnar Chasers visíveis pelo player.
/// </summary>
[CreateAssetMenu(fileName = "ChaserPoolSettings", menuName = "Movement/AI/Chaser Pool Settings")]
public class ChaserPoolSettings : ScriptableObject
{
    [Header("Contagem")]
    [Tooltip("Quantos Chasers devem estar ativos (fora de Resting) ao redor do player a qualquer momento.")]
    public int desiredActiveCount = 3;

    [Header("Distâncias de spawn (anel ao redor do player)")]
    public float spawnMinDistance = 10f;
    public float spawnMaxDistance = 20f;

    [Header("Retorno à pool")]
    [Tooltip("Deve ser bem maior que spawnMaxDistance - cria uma margem de segurança contra spawn/retorno oscilando em sequência.")]
    public float returnDistance = 35f;

    [Header("Campo de visão do player (aproximação simplificada por distância)")]
    [Tooltip("Distância à frente do player que define o centro da 'zona de visão'.")]
    public float fieldOfViewForwardOffset = 8f;

    [Tooltip("Raio da zona de visão - candidatos de spawn dentro dela são descartados por estarem visíveis.")]
    public float fieldOfViewRadius = 12f;

    [Header("Amostragem")]
    public int maxSampleAttempts = 30;

    [Tooltip("Intervalo, em segundos, entre cada avaliação de retorno/spawn da pool.")]
    public float evaluationInterval = 1f;
}