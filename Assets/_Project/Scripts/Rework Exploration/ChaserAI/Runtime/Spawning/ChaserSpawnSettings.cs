using UnityEngine;

/// <summary>
/// Tuning de spawn e realocação: anel ao redor do player, distância máxima antes de
/// realocar, aproximação de campo de visão e ritmo de colocação. Substitui o antigo
/// ChaserPoolSettings (mesmo asset/GUID; campos com o mesmo nome mantêm seus valores).
/// </summary>
[CreateAssetMenu(fileName = "ChaserSpawnSettings", menuName = "Movement/AI/Chaser Spawn Settings")]
public class ChaserSpawnSettings : ScriptableObject
{
    [Header("Anel de spawn ao redor do player")]
    public float spawnMinDistance = 15f;
    public float spawnMaxDistance = 30f;

    [Header("Realocação")]
    [Tooltip("Chaser mais longe que isso do player é realocado. Mantenha bem acima de " +
             "spawnMaxDistance para não oscilar entre realocações.")]
    public float returnDistance = 45f;

    [Tooltip("Intervalo, em segundos, entre cada avaliação de distância.")]
    public float evaluationInterval = 1f;

    [Header("Campo de visão do player (aproximação por distância)")]
    [Tooltip("Distância à frente do player que define o centro da 'zona de visão'.")]
    public float fieldOfViewForwardOffset = 8f;

    [Tooltip("Raio da zona de visão: pontos dentro dela são descartados por estarem visíveis.")]
    public float fieldOfViewRadius = 12f;

    [Header("Amostragem")]
    [Tooltip("Tentativas de sortear um ponto válido por chaser, por rodada.")]
    public int maxSampleAttempts = 30;

    [Header("Colocação inicial")]
    [Tooltip("Quantos chasers ativar por frame no começo, para não travar com populações grandes.")]
    public int placementsPerFrame = 10;

    [Tooltip("Espera, em segundos, antes de tentar de novo quando uma rodada não achou nenhum ponto válido.")]
    public float placementRetryInterval = 0.5f;

    private void OnValidate()
    {
        spawnMinDistance = Mathf.Max(0f, spawnMinDistance);
        spawnMaxDistance = Mathf.Max(spawnMinDistance + 1f, spawnMaxDistance);
        returnDistance = Mathf.Max(spawnMaxDistance + 5f, returnDistance);
        evaluationInterval = Mathf.Max(0.1f, evaluationInterval);
        maxSampleAttempts = Mathf.Max(1, maxSampleAttempts);
        placementsPerFrame = Mathf.Max(1, placementsPerFrame);
        placementRetryInterval = Mathf.Max(0.05f, placementRetryInterval);
    }
}
