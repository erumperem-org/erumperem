using UnityEngine;

/// <summary>
/// Tuning compartilhado entre todos os PlayableCharacters: comportamento de
/// seguir do Companheiro (mesmo espírito do ChaserSettings) e distância de
/// chegada no ponto de Resting.
/// </summary>
[CreateAssetMenu(fileName = "PlayableCharacterSettings", menuName = "Movement/Playable Character Settings")]
public class PlayableCharacterSettings : ScriptableObject
{
    [Header("Companheiro - Seguir")]
    [Tooltip("Distância na qual o companheiro para de se mover em direção ao líder.")]
    public float stopDistance = 1.5f;

    [Tooltip("Distância a partir da qual o companheiro liga o sprint para alcançar o líder.")]
    public float catchUpTriggerDistance = 6f;

    [Tooltip("Distância na qual o companheiro desliga o sprint, após ter alcançado o líder de perto o suficiente. Deve ser menor que catchUpTriggerDistance.")]
    public float catchUpRecoverDistance = 3f;

    [Header("Desvio de obstáculo (Companheiro)")]
    public float maxAvoidanceAngle = 90f;
    public float avoidanceAngleStep = 15f;

    [Header("Resting")]
    [Tooltip("Distância a partir da qual o personagem é considerado 'chegou' no ponto de Resting.")]
    public float restingArrivalThreshold = 0.5f;

    [Header("Companheiro - Abrir Passagem")]
    [Tooltip("Distância abaixo da qual o companheiro para de tentar se aproximar e passa a se deslocar lateralmente, para não bloquear o caminho do líder. Deve ser menor que stopDistance.")]
    public float pathClearDistance = 0.8f;
}
