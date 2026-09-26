using UnityEngine;
using Unity.Cinemachine;

[DisallowMultipleComponent]
public sealed class TorchThreatCameraShake : CinemachineExtension
{
    private float _startedAt;
    private float _duration;
    private float _strength;
    private float _proximityStrength;

    public void SetProximityStrength(float strength) => _proximityStrength = Mathf.Max(0f, strength);

    public void Play(float duration, float strength)
    {
        _startedAt = Time.time;
        _duration = Mathf.Max(0.01f, duration);
        _strength = strength;
    }

    public void Stop() => _duration = 0f;

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Noise || deltaTime < 0f) return;
        float elapsed = Time.time - _startedAt;
        float strength = _proximityStrength;
        if (_duration > 0f && elapsed < _duration)
            strength = Mathf.Max(strength, _strength * Mathf.Pow(1f - elapsed / _duration, 2f));
        if (strength <= 0f) return;
        state.OrientationCorrection *= Quaternion.Euler(
            Mathf.Sin(Time.time * 71f) * strength,
            Mathf.Sin(Time.time * 53f) * strength * 0.5f, 0f);
    }
}
