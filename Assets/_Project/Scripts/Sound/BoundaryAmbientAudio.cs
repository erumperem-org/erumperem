using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MeshCollider))]
public sealed class BoundaryAmbientAudio : MonoBehaviour
{
    [SerializeField] private string _soundName = "CorruptionBarrier";
    [SerializeField, Min(0.1f)] private float _audibleDistance = 15f;
    [SerializeField, Min(0.01f)] private float _fadeDuration = 0.3f;

    private MeshCollider _boundary;
    private AudioSource _source;
    private PlayableCharactersManager _legacyCharacters;
    private PlayableCharacterController _characters;
    private float _clipVolume;
    private float _nextResolveTime;
    private bool _configured;

    private void Awake()
    {
        _boundary = GetComponent<MeshCollider>();
        var emitter = new GameObject("BoundaryAudioEmitter");
        emitter.transform.SetParent(transform, false);
        _source = emitter.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = true;
        _source.spatialBlend = 1f;
        _source.spread = 0f;
        _source.dopplerLevel = 0f;
        _source.rolloffMode = AudioRolloffMode.Custom;
        _source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0f, 1f, 1f, 1f));
        _source.volume = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime >= _nextResolveTime)
        {
            _nextResolveTime = Time.unscaledTime + 1f;
            if (_characters == null) _characters = FindFirstObjectByType<PlayableCharacterController>();
            if (_legacyCharacters == null) _legacyCharacters = FindFirstObjectByType<PlayableCharactersManager>();
            if (!_configured && AudioManager.instance != null)
            {
                _configured = AudioManager.instance.TryConfigureAmbientSource(_soundName, _source);
                if (_configured)
                {
                    _clipVolume = _source.volume;
                    _source.volume = 0f;
                }
            }
        }

        if (!_configured) return;
        var player = _characters != null && _characters.InGameCharacter != null
            ? _characters.InGameCharacter.transform : _legacyCharacters?.Main?.Transform;
        float volume = 0f;
        if (player != null && _boundary.enabled && TryGetBoundaryPoint(player.position, out var point))
        {
            _source.transform.position = point;
            float proximity = Mathf.Clamp01(1f - Vector3.Distance(player.position, point) / _audibleDistance);
            volume = _clipVolume * Mathf.SmoothStep(0f, 1f, proximity);
        }

        _source.volume = Mathf.MoveTowards(_source.volume, volume,
            Time.unscaledDeltaTime * _clipVolume / Mathf.Max(0.01f, _fadeDuration));
        if (_source.volume > 0f && !_source.isPlaying) _source.Play();
        else if (_source.volume <= 0f && _source.isPlaying) _source.Stop();
    }

    private bool TryGetBoundaryPoint(Vector3 player, out Vector3 point)
    {
        var bounds = _boundary.bounds;
        var origin = bounds.center;
        origin.y = Mathf.Clamp(player.y, bounds.min.y + 0.01f, bounds.max.y - 0.01f);
        var direction = Vector3.ProjectOnPlane(player - bounds.center, Vector3.up).normalized;
        point = default;
        if (direction.sqrMagnitude < 0.001f) return false;

        float length = bounds.extents.magnitude + 1f;
        bool innerHit = _boundary.Raycast(new Ray(origin, direction), out var inner, length);
        bool outerHit = _boundary.Raycast(new Ray(origin + direction * length, -direction), out var outer, length);
        if (!innerHit && !outerHit) return false;
        point = !outerHit || (innerHit && (inner.point - player).sqrMagnitude < (outer.point - player).sqrMagnitude)
            ? inner.point : outer.point;
        return true;
    }

    private void OnDisable()
    {
        if (_source == null) return;
        _source.Stop();
        _source.volume = 0f;
    }
}
