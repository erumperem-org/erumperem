using System;
using UnityEngine;
using BarSystem.Bars.Health;

/// <summary>
/// Um personagem jogável, com 3 papéis possíveis (Em Jogo, Companheiro,
/// Resting). Nunca é instanciado/destruído - existe fixo na cena e só troca
/// de papel via SetState, chamado exclusivamente pelo
/// PlayableCharacterController (nunca diretamente por outro código).
/// </summary>
[RequireComponent(typeof(PhysicsMovementService))]
public class PlayableCharacters : MonoBehaviour
{
    private const string InGameTag = "Player";
    private const string OtherRolesTag = "NPC";

    [Header("Identidade")]
    [Tooltip("Identificador único do personagem, usado pelo save/load. " +
             "Por enquanto é uma string livre definida no Inspector - " +
             "deve ser substituído pela fonte externa de identidade de " +
             "personagens quando ela existir (ver README).")]
    [SerializeField] private string characterId;

    [Header("Configuração")]
    [SerializeField] private PlayableCharacterSettings settings;

    [Header("Vida")]
    [SerializeField] private PlayableCharacterHealthBarInstaller healthBar;
    public PlayableCharacterHealthBarInstaller HealthBar => healthBar;

    [Header("Ponto de descanso")]
    [Tooltip("Posição para onde este personagem caminha ao entrar em Resting.")]
    [SerializeField] private Transform restingPoint;
    public Transform RestingPoint => restingPoint;

    [Header("Componentes de comportamento (um ativo por vez)")]
    [SerializeField] private PlayerInputMovementController playerInputController;
    [SerializeField] private CompanionFollowController companionFollowController;
    [SerializeField] private RestingMovementController restingMovementController;

    public string CharacterId => characterId;
    public CharacterState CurrentState { get; private set; }

    /// <summary>
    /// Disparado quando este personagem chega fisicamente ao ponto de
    /// Resting e desliga sua própria movimentação. Uso local (ex: sistemas
    /// de interação no mesmo personagem) - diferente dos eventos de papel
    /// do PlayableCharacterController, que disparam no momento da decisão,
    /// não da chegada física.
    /// </summary>
    public event Action OnArrivedAtRestingPoint;

    private void Awake()
    {
        companionFollowController.Initialize(settings);
        restingMovementController.Initialize(restingPoint, settings, HandleArrivedAtRestingPoint);

        playerInputController.enabled = false;
        companionFollowController.enabled = false;
        restingMovementController.enabled = false;
    }

    /// <summary>Único ponto de troca de papel. Chamado exclusivamente pelo PlayableCharacterController.</summary>
    public void SetState(CharacterState newState)
    {
        CurrentState = newState;

        if (newState == CharacterState.Resting)
        {
            restingMovementController.BeginResting();
        }

        playerInputController.enabled = newState == CharacterState.InGame;
        companionFollowController.enabled = newState == CharacterState.Companion;
        restingMovementController.enabled = newState == CharacterState.Resting;

        gameObject.tag = newState == CharacterState.InGame ? InGameTag : OtherRolesTag;
    }

    /// <summary>Usado pelo PlayableCharacterController para manter o Companheiro seguindo o personagem Em Jogo correto.</summary>
    public void SetFollowTarget(Transform target)
    {
        companionFollowController.SetFollowTarget(target);
    }

    /// <summary>Usado pelo save/load para reposicionar o personagem diretamente, sem passar pela movimentação normal.</summary>
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

    private void HandleArrivedAtRestingPoint()
    {
        OnArrivedAtRestingPoint?.Invoke();
    }
}
