using Unity.Cinemachine;
using UnityEngine;

public class CinemachineCameraTargetUpdate : MonoBehaviour
{
    public PlayableCharactersManager manager;
    [SerializeField] private CinemachineCamera _cinemachineCamera;
    void Awake()
    {
        manager.MainCharacterChange += SetTarget;
        if (_cinemachineCamera == null)
        {
            this.GetComponent<CinemachineCamera>();
        }
    }

    public void SetTarget(PlayableCharacter main)
    {
        _cinemachineCamera.Target.TrackingTarget = main.transform;
    }

    public void SetFollowAndLookAt(Transform follow, Transform lookAt)
    {
        _cinemachineCamera.Target.TrackingTarget = follow;
        _cinemachineCamera.Target.CustomLookAtTarget = true;
        _cinemachineCamera.Target.LookAtTarget = lookAt;
    }
}
