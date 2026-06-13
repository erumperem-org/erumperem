using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Atualiza o target da câmera Cinemachine quando o Main muda.
/// Depende do evento <see cref="PlayableCharactersManager.OnMainChanged"/>
/// e da interface <see cref="IPlayableCharacter"/> — nenhuma referência ao concreto.
/// </summary>
[RequireComponent(typeof(CinemachineCamera))]
public sealed class CinemachineCameraTargetUpdate : MonoBehaviour
{
    [SerializeField] private PlayableCharactersManager _manager;

    private CinemachineCamera _camera;

    private void Awake()
    {
        _camera = GetComponent<CinemachineCamera>();
        if (_manager == null)
        {
            _manager = FindFirstObjectByType<PlayableCharactersManager>();
        }

        if (_manager == null)
        {
            Debug.LogError("[CinemachineCameraTargetUpdate] PlayableCharactersManager não encontrado na cena.", this);
            enabled = false;
            return;
        }

        _manager.OnMainChanged += OnMainChanged;
    }

    private void OnDestroy()
    {
        if (_manager != null)
        {
            _manager.OnMainChanged -= OnMainChanged;
        }
    }

    private void OnMainChanged(IPlayableCharacter main)
    {
        if (main == null) return;
        _camera.Target.TrackingTarget = main.Transform;
    }

    /// <summary>Sobrescreve follow e lookAt manualmente (ex: câmera de combate).</summary>
    public void SetFollowAndLookAt(Transform follow, Transform lookAt)
    {
        _camera.Target.TrackingTarget     = follow;
        _camera.Target.CustomLookAtTarget = true;
        _camera.Target.LookAtTarget       = lookAt;
    }
}
