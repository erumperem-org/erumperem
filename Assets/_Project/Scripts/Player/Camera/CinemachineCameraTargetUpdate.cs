using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Mantém a câmera Cinemachine focada no personagem Main.
/// Ouve <see cref="PlayableCharactersManager.OnMainChanged"/> e re-sincroniza após load
/// ou quando o alvo serializado na cena deixa de coincidir com o Main actual.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
[DefaultExecutionOrder(100)]
public sealed class CinemachineCameraTargetUpdate : MonoBehaviour
{
    [SerializeField] private PlayableCharactersManager _manager;

    private CinemachineCamera _camera;

    private void Awake()
    {
        _camera = GetComponent<CinemachineCamera>();
        ResolveManagerReference();
    }

    private void OnEnable()
    {
        ResolveManagerReference();

        if (_manager == null)
        {
            return;
        }

        _manager.OnMainChanged += OnMainChanged;
        SyncCameraToCurrentMain();
    }

    private void OnDisable()
    {
        if (_manager != null)
        {
            _manager.OnMainChanged -= OnMainChanged;
        }
    }

    private void Start() => SyncCameraToCurrentMain();

    private void LateUpdate() => SyncCameraToCurrentMainIfDrifted();

    private void ResolveManagerReference()
    {
        if (_manager != null)
        {
            return;
        }

        _manager = FindFirstObjectByType<PlayableCharactersManager>();
        if (_manager == null)
        {
            Debug.LogError("[CinemachineCameraTargetUpdate] PlayableCharactersManager não encontrado na cena.", this);
            enabled = false;
        }
    }

    private void OnMainChanged(IPlayableCharacter main)
    {
        if (main == null)
        {
            return;
        }

        ApplyExplorationTrackingTarget(main.Transform);
    }

    private void SyncCameraToCurrentMain()
    {
        if (_manager?.Main == null)
        {
            return;
        }

        ApplyExplorationTrackingTarget(_manager.Main.Transform);
    }

    private void SyncCameraToCurrentMainIfDrifted()
    {
        if (_manager?.Main == null)
        {
            return;
        }

        var mainTransform = _manager.Main.Transform;
        if (_camera.Target.TrackingTarget == mainTransform
            && !_camera.Target.CustomLookAtTarget)
        {
            return;
        }

        ApplyExplorationTrackingTarget(mainTransform);
    }

    private void ApplyExplorationTrackingTarget(Transform mainCharacterTransform)
    {
        if (mainCharacterTransform == null)
        {
            return;
        }

        _camera.Target.CustomLookAtTarget = false;
        _camera.Target.LookAtTarget = null;
        _camera.Target.TrackingTarget = mainCharacterTransform;
    }

    /// <summary>Sobrescreve follow e lookAt manualmente (ex: câmera de combate).</summary>
    public void SetFollowAndLookAt(Transform follow, Transform lookAt)
    {
        _camera.Target.TrackingTarget = follow;
        _camera.Target.CustomLookAtTarget = true;
        _camera.Target.LookAtTarget = lookAt;
    }
}
