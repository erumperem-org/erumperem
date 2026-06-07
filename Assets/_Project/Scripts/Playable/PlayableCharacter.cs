using Player;
using UnityEngine;

/// <summary>
/// Dados e referências de um personagem jogável. Data-holder puro.
///
/// ADIÇÃO em relação à versão anterior:
///   - <c>PlayerInput</c> exposto como <c>internal</c> para que
///     <see cref="PlayerDetectionSystem"/> possa montar o <see cref="InteractionContext"/>
///     com o callback de bloqueio de input — sem que sistemas externos acessem o reader.
/// </summary>
[RequireComponent(typeof(PlayerMovementController))]
public sealed class PlayableCharacter : MonoBehaviour, IPlayableCharacter
{
    [Header("Identificação")]
    [SerializeField] private string _characterName;
    [SerializeField] private Sprite _icon;

    [Header("Sub-systems")]
    [SerializeField] private PlayerMovementController    _movementController;
    [SerializeField] private PlayerDetectionSystem       _detectionSystem;
    [SerializeField] private PlayableAnimationController _animationController;
    [SerializeField] private PlayerInputReader           _playerInput;
    public PlayableHealthBar HealthBar;

    [Header("Resting")]
    [SerializeField] private Transform _restingPoint;

    // ── IPlayableCharacter ────────────────────────────────────────────────
    public string    CharacterName => _characterName;
    public Sprite    Icon          => _icon;
    public Transform Transform     => transform;

    public PlayableCharacterState CurrentState { get; internal set; } = PlayableCharacterState.None;
    public PlayableCharacterState CurrentStateExposed;

    // ── Acesso interno ────────────────────────────────────────────────────
    internal PlayerMovementController    MovementController  => _movementController;
    internal PlayerDetectionSystem       DetectionSystem     => _detectionSystem;
    internal PlayableAnimationController AnimationController => _animationController;
    internal PlayerInputReader           PlayerInput         => _playerInput;
    internal Transform                   RestingPoint        => _restingPoint;

    private void Reset()
    {
        _movementController  = GetComponent<PlayerMovementController>();
        _detectionSystem     = GetComponent<PlayerDetectionSystem>();
        _animationController = GetComponent<PlayableAnimationController>();
        _playerInput         = GetComponent<PlayerInputReader>();
    }

    public void UpdateStateExposed()
    {
        CurrentStateExposed = CurrentState;
    }
}
