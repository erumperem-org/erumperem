using UnityEngine;

/// <summary>
/// Tuning do ChaserPool: distâncias de reposicionamento (com margem de
/// histerese entre elas) e a aproximação simplificada de "campo de visão"
/// usada para não reposicionar um Chaser onde o player o veria reaparecer.
///
/// Não existe mais uma contagem de "ativos desejados": todo ChaserAI da
/// lista (ver ChaserPool) fica sempre ativo (Wandering/Chasing/
/// Investigating). O papel do ChaserPool agora é só trazer de volta, para
/// perto do player, qualquer Chaser que se afaste demais - permitindo que
/// poucos Chasers povoem um mapa grande.
/// </summary>
[CreateAssetMenu(fileName = "ChaserPoolSettings", menuName = "Movement/AI/Chaser Pool Settings")]
public class ChaserPoolSettings : ScriptableObject
{
    [Header("Distâncias de reposicionamento (anel ao redor do player)")]
    public float spawnMinDistance = 10f;
    public float spawnMaxDistance = 20f;

    [Header("Distância de reposicionamento")]
    [Tooltip("Deve ser bem maior que spawnMaxDistance - cria uma margem de segurança contra reposicionar o mesmo Chaser em sequência, oscilando.")]
    public float returnDistance = 35f;

    [Header("Campo de visão do player (aproximação simplificada por distância)")]
    [Tooltip("Distância à frente do player que define o centro da 'zona de visão'.")]
    public float fieldOfViewForwardOffset = 8f;

    [Tooltip("Raio da zona de visão - candidatos de reposicionamento dentro dela são descartados por estarem visíveis.")]
    public float fieldOfViewRadius = 12f;

    [Header("Amostragem")]
    public int maxSampleAttempts = 30;

    [Tooltip("Intervalo, em segundos, entre cada avaliação de reposicionamento da pool.")]
    public float evaluationInterval = 1f;
}