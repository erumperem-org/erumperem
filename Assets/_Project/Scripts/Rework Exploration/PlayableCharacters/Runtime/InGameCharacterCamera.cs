using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Mantém a câmera Cinemachine focada no personagem Em Jogo. Ouve
/// <see cref="PlayableCharacterController.OnCharacterEnteredInGame"/> e
/// re-sincroniza após load (ou sempre que o alvo atual da câmera deixar de
/// coincidir com o personagem Em Jogo do controlador, ex: troca causada por
/// algo que não passou pelo evento).
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
[DefaultExecutionOrder(100)]
public sealed class InGameCharacterCamera : MonoBehaviour
{
    [SerializeField] private PlayableCharacterController _controller;

    private CinemachineCamera _camera;

    private void Awake()
    {
        _camera = GetComponent<CinemachineCamera>();
        ResolveControllerReference();
    }

    private void OnEnable()
    {
        ResolveControllerReference();

        if (_controller == null)
        {
            return;
        }

        _controller.OnCharacterEnteredInGame += OnCharacterEnteredInGame;
        SyncCameraToCurrentInGameCharacter();
    }

    private void OnDisable()
    {
        if (_controller != null)
        {
            _controller.OnCharacterEnteredInGame -= OnCharacterEnteredInGame;
        }
    }

    private void Start() => SyncCameraToCurrentInGameCharacter();

    private void LateUpdate() => SyncCameraToCurrentInGameCharacterIfDrifted();

    private void ResolveControllerReference()
    {
        if (_controller != null)
        {
            return;
        }

        _controller = FindFirstObjectByType<PlayableCharacterController>();
        if (_controller == null)
        {
            Debug.LogError("[CinemachineCameraTargetUpdate] PlayableCharacterController não encontrado na cena.", this);
            enabled = false;
        }
    }

    private void OnCharacterEnteredInGame(PlayableCharacters character)
    {
        if (character == null)
        {
            return;
        }

        ApplyExplorationTrackingTarget(character.transform);
    }

    private void SyncCameraToCurrentInGameCharacter()
    {
        var inGameCharacter = _controller != null ? _controller.InGameCharacter : null;

        if (inGameCharacter == null)
        {
            return;
        }

        ApplyExplorationTrackingTarget(inGameCharacter.transform);
    }

    private void SyncCameraToCurrentInGameCharacterIfDrifted()
    {
        var inGameCharacter = _controller != null ? _controller.InGameCharacter : null;

        if (inGameCharacter == null)
        {
            return;
        }

        var inGameTransform = inGameCharacter.transform;
        if (_camera.Target.TrackingTarget == inGameTransform
            && !_camera.Target.CustomLookAtTarget)
        {
            return;
        }

        ApplyExplorationTrackingTarget(inGameTransform);
    }

    private void ApplyExplorationTrackingTarget(Transform inGameCharacterTransform)
    {
        if (inGameCharacterTransform == null)
        {
            return;
        }

        _camera.Target.CustomLookAtTarget = false;
        _camera.Target.LookAtTarget = null;
        _camera.Target.TrackingTarget = inGameCharacterTransform;
    }

    /// <summary>Sobrescreve follow e lookAt manualmente (ex: câmera de combate).</summary>
    public void SetFollowAndLookAt(Transform follow, Transform lookAt)
    {
        _camera.Target.TrackingTarget = follow;
        _camera.Target.CustomLookAtTarget = true;
        _camera.Target.LookAtTarget = lookAt;
    }
}