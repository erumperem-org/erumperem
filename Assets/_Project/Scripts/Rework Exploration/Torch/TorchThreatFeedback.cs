using System.Collections;
using UnityEngine;
using Unity.Cinemachine;
using TMPro;
using DG.Tweening;

[DisallowMultipleComponent]
public sealed class TorchThreatFeedback : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _cooldownSeconds = 5f;
    [SerializeField, Min(0f)] private float _warningDelaySeconds = 2.5f;
    [SerializeField, Min(0.1f)] private float _messageDurationSeconds = 3f;
    [SerializeField, Min(0f)] private float _musicFadeSeconds = 1.2f;
    [SerializeField] private string _screamSfx = "DistantScreams";
    [SerializeField] private string _threatPlaylist = "TorchThreat";
    [SerializeField, Min(0f)] private float _villageProtectionMargin = 5f;
    [SerializeField] private string _villagePlaylist = "VillageMusic";
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField] private Color _accent = new Color(0.65f, 0.035f, 0.025f);
    [SerializeField, Range(0f, 2f)] private float _shakeStrength = 0.35f;
    [SerializeField, Range(0f, 0.2f)] private float _pulseOpacity = 0.035f;
    [SerializeField] private Color _pulseColor = new Color(0.35f, 0.08f, 0.07f);
    [Header("Pursuit Shake")]
    [SerializeField, Min(0f)] private float _pursuitShakeStartDistance = 16f;
    [SerializeField, Min(0f)] private float _pursuitShakeFullDistance = 2f;
    [SerializeField, Range(0f, 2f)] private float _pursuitShakeStrength = 0.3f;
    [SerializeField, Min(0.01f)] private float _pursuitShakeResponseSeconds = 0.6f;

    private static TorchThreatFeedback _instance;
    private Canvas _canvas;
    private CanvasGroup _warning;
    private CanvasGroup _cooldown;
    private TextMeshProUGUI _cooldownText;
    private UnityEngine.UI.Image _pulse;
    private Sequence _warningTween;
    private Tween _cooldownTween;
    private Coroutine _musicRoutine;
    private TorchThreatCameraShake _shake;
    private Hub _hub;
    private VillageArea _village;
    private bool _torchLit;
    private bool _warned;
    private float _exposureTime;
    private float _nextToggleAt;
    private AudioManager _audio;
    private int _musicRevision = -1;
    private string _previousPlaylist;
    private PlayableCharactersManager _legacyCharacters;
    private PlayableCharacterController _characters;
    private SphereCollider _villageSphere;
    private bool? _wasInVillage;
    private bool _musicReady;
    private Coroutine _villageMusicRoutine;
    private AudioManager _villageAudio;
    private int _villageMusicRevision = -1;
    private string _outsidePlaylist;
    private bool _hasVillageMusicOverride;
    private float _blockedNoticeUntil;
    private bool _referencesResolved;
    private ChaserAI[] _chasers = System.Array.Empty<ChaserAI>();
    private Systems.NPC.Enemy.NpcEnemy[] _legacyEnemies = System.Array.Empty<Systems.NPC.Enemy.NpcEnemy>();
    private float _nextEnemyRefresh;
    private float _pursuitIntensity;

    public bool CanLightTorch => !IsWithinVillage(_villageProtectionMargin);

    public static TorchThreatFeedback Ensure(GameObject owner)
    {
        if (_instance != null) return _instance;
        return owner.GetComponent<TorchThreatFeedback>() ?? owner.AddComponent<TorchThreatFeedback>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }
        _instance = this;
        ResolveReferences();
        BuildInterface();
    }

    private void ResolveReferences()
    {
        if (_referencesResolved) return;
        _hub = FindFirstObjectByType<Hub>();
        _village = FindFirstObjectByType<VillageArea>();
        _villageSphere = _village != null ? _village.GetComponent<SphereCollider>() : null;
        _legacyCharacters = FindFirstObjectByType<PlayableCharactersManager>();
        _characters = FindFirstObjectByType<PlayableCharacterController>();
        _referencesResolved = true;
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return null;
        _musicReady = true;
    }

    private bool IsWithinVillage(float margin)
    {
        ResolveReferences();
        var player = _characters != null && _characters.InGameCharacter != null
            ? _characters.InGameCharacter.transform : _legacyCharacters?.Main?.Transform;
        if (player == null) return false;
        if (_hub != null && _hub.isActiveAndEnabled)
        {
            var delta = player.position - _hub.Center;
            delta.y = 0f;
            if (delta.sqrMagnitude <= Mathf.Pow(_hub.Radius + margin, 2f)) return true;
        }
        if (_villageSphere == null || !_villageSphere.enabled || !_village.isActiveAndEnabled) return false;
        var scale = _villageSphere.transform.lossyScale;
        float radius = _villageSphere.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)) + margin;
        var offset = player.position - _villageSphere.transform.TransformPoint(_villageSphere.center);
        offset.y = 0f;
        return offset.sqrMagnitude <= radius * radius;
    }

    public bool TryToggle(bool lightTorch)
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0f) return false;
        if (lightTorch && !CanLightTorch)
        {
            _blockedNoticeUntil = Time.time + 2f;
            _cooldownTween?.Kill();
            _cooldownText.text = "I'm protected here, there's no need for the flame.";
            _cooldown.alpha = 1f;
            return false;
        }
        if (Time.time < _nextToggleAt)
        {
            _cooldownTween?.Kill();
            _cooldown.alpha = 1f;
            return false;
        }
        _nextToggleAt = Time.time + _cooldownSeconds;
        _cooldownTween?.Kill();
        _cooldown.alpha = 1f;
        return true;
    }

    public void SetTorchLit(bool lit)
    {
        if (_torchLit == lit) return;
        _torchLit = lit;
        if (!lit) ClearThreat();
    }

    private void Update()
    {
        float remaining = _nextToggleAt - Time.time;
        if (Time.time < _blockedNoticeUntil)
            _cooldownText.text = "Within these wards, your torch must rest.";
        else if (remaining > 0f)
            _cooldownText.text = $"Let the flame settle... {Mathf.CeilToInt(remaining)}s";
        else if (_cooldown.alpha > 0f && (_cooldownTween == null || !_cooldownTween.IsActive()))
            _cooldownTween = _cooldown.DOFade(0f, 0.3f);

        bool safe = IsWithinVillage(0f);
        if (safe && _torchLit)
        {
            if (TorchManager.Instance != null) TorchManager.Instance.SetTorchState(false);
            var main = _legacyCharacters?.Main?.Transform;
            if (main != null) main.GetComponent<Player.PlayerTorchHandler>()?.Extinguish();
            SetTorchLit(false);
        }
        UpdatePursuitShake(safe);
        if (safe && (_warned || _exposureTime > 0f)) ClearThreat();
        UpdateVillageMusic(safe);
        if (!_torchLit || safe)
        {
            if (_warned || _exposureTime > 0f) ClearThreat();
            return;
        }
        if (_warned || _villageMusicRoutine != null) return;
        _exposureTime += Time.deltaTime;
        if (_exposureTime >= _warningDelaySeconds) ShowThreat();
    }

    private void UpdatePursuitShake(bool safe)
    {
        if (Time.time >= _nextEnemyRefresh)
        {
            _nextEnemyRefresh = Time.time + 0.5f;
            _chasers = FindObjectsByType<ChaserAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _legacyEnemies = FindObjectsByType<Systems.NPC.Enemy.NpcEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        var player = _characters != null && _characters.InGameCharacter != null
            ? _characters.InGameCharacter.transform : _legacyCharacters?.Main?.Transform;
        float nearestDistance = float.PositiveInfinity;
        if (!safe && player != null)
        {
            foreach (var enemy in _chasers)
            {
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.CurrentState != ChaserState.Chasing
                    || enemy.Target == null || (enemy.Target != player && !enemy.Target.IsChildOf(player))) continue;
                nearestDistance = Mathf.Min(nearestDistance, Vector3.Distance(player.position, enemy.transform.position));
            }
            foreach (var enemy in _legacyEnemies)
            {
                if (enemy == null || !enemy.isActiveAndEnabled
                    || enemy.CurrentState != Systems.NPC.Enemy.Contracts.NpcEnemyState.Chase) continue;
                nearestDistance = Mathf.Min(nearestDistance, Vector3.Distance(player.position, enemy.transform.position));
            }
        }

        float startDistance = Mathf.Max(_pursuitShakeFullDistance + 0.01f, _pursuitShakeStartDistance);
        float proximity = 1f - Mathf.InverseLerp(_pursuitShakeFullDistance, startDistance, nearestDistance);
        float target = Mathf.SmoothStep(0f, 1f, proximity) * _pursuitShakeStrength;
        _pursuitIntensity = Mathf.MoveTowards(_pursuitIntensity, target,
            Time.deltaTime * Mathf.Max(0.01f, _pursuitShakeStrength, _pursuitIntensity)
            / Mathf.Max(0.01f, _pursuitShakeResponseSeconds));
        if (_pursuitIntensity > 0f) ResolveCameraShake();
        if (_shake != null) _shake.SetProximityStrength(_pursuitIntensity);
    }

    private void ResolveCameraShake()
    {
        var camera = Camera.main;
        var brain = camera != null ? camera.GetComponent<CinemachineBrain>() : null;
        if (brain == null || brain.ActiveVirtualCamera is not CinemachineVirtualCameraBase virtualCamera) return;
        if (_shake != null && _shake.gameObject == virtualCamera.gameObject) return;
        if (_shake != null)
        {
            _shake.SetProximityStrength(0f);
            _shake.Stop();
        }
        _shake = virtualCamera.GetComponent<TorchThreatCameraShake>() ?? virtualCamera.gameObject.AddComponent<TorchThreatCameraShake>();
    }

    private void UpdateVillageMusic(bool inside)
    {
        if (!_musicReady || AudioManager.instance == null || _wasInVillage == inside) return;
        bool wasInside = _wasInVillage == true;
        _wasInVillage = inside;
        if (_villageMusicRoutine != null) StopCoroutine(_villageMusicRoutine);
        _villageMusicRoutine = null;
        if (inside)
        {
            _villageAudio = AudioManager.instance;
            if (!_villageAudio.HasBGM(_villagePlaylist)) return;
            if (!_hasVillageMusicOverride)
                _outsidePlaylist = _villageAudio.CurrentPlaylistName;
            _hasVillageMusicOverride = true;
            _villageMusicRoutine = StartCoroutine(SwitchVillageMusic(_villagePlaylist));
        }
        else if (wasInside && _villageAudio != null && _villageAudio.BgmRevision == _villageMusicRevision)
        {
            _villageMusicRoutine = StartCoroutine(SwitchVillageMusic(_outsidePlaylist));
        }
    }

    private IEnumerator SwitchVillageMusic(string playlist)
    {
        _villageAudio.FadeOutBGM(_musicFadeSeconds);
        _villageMusicRevision = _villageAudio.BgmRevision;
        yield return new WaitForSecondsRealtime(_musicFadeSeconds);
        if (_villageAudio != null && _villageAudio.BgmRevision == _villageMusicRevision)
        {
            if (_villageAudio.HasBGM(playlist)) _villageAudio.FadeInBGM(playlist);
            _villageMusicRevision = _villageAudio.BgmRevision;
        }
        if (_wasInVillage != true) _hasVillageMusicOverride = false;
        _villageMusicRoutine = null;
    }

    private void ShowThreat()
    {
        _warned = true;
        _warningTween?.Kill();
        _warning.alpha = 0f;
        _warning.transform.localScale = Vector3.one * 0.94f;
        _pulse.color = new Color(_pulseColor.r, _pulseColor.g, _pulseColor.b, 0f);
        _warningTween = DOTween.Sequence()
            .Append(_warning.DOFade(1f, 0.3f))
            .Join(_warning.transform.DOScale(1f, 0.5f).SetEase(Ease.OutCubic))
            .Join(_pulse.DOFade(_pulseOpacity, 0.2f))
            .Append(_pulse.DOFade(0f, 0.7f))
            .AppendInterval(_messageDurationSeconds)
            .Append(_warning.DOFade(0f, 0.5f));

        ResolveCameraShake();
        if (_shake != null) _shake.Play(0.6f, _shakeStrength);
        _audio = AudioManager.instance;
        if (_audio == null) return;
        _audio.PlaySFX(_screamSfx);
        _previousPlaylist = _audio.bgmSource != null && _audio.bgmSource.isPlaying ? _audio.CurrentPlaylistName : null;
        _audio.FadeOutBGM(_musicFadeSeconds);
        _musicRevision = _audio.BgmRevision;
        _musicRoutine = StartCoroutine(StartThreatMusic());
    }

    private IEnumerator StartThreatMusic()
    {
        yield return new WaitForSecondsRealtime(_musicFadeSeconds);
        if (_audio != null && _audio.BgmRevision == _musicRevision && _audio.HasBGM(_threatPlaylist))
        {
            _audio.FadeInBGM(_threatPlaylist);
            _musicRevision = _audio.BgmRevision;
        }
        _musicRoutine = null;
    }

    private void ClearThreat()
    {
        _warned = false;
        _exposureTime = 0f;
        if (_musicRoutine != null) StopCoroutine(_musicRoutine);
        _musicRoutine = null;
        if (_audio != null && _audio.BgmRevision == _musicRevision)
        {
            if (!string.IsNullOrEmpty(_previousPlaylist)) _audio.FadeInBGM(_previousPlaylist);
            else _audio.FadeOutBGM(_musicFadeSeconds);
        }
        _musicRevision = -1;
        _previousPlaylist = null;
        _warningTween?.Kill();
        if (_warning != null) _warning.alpha = 0f;
        if (_pulse != null) _pulse.color = Color.clear;
        if (_shake != null) _shake.Stop();
    }

    private void BuildInterface()
    {
        var root = new GameObject("TorchThreatCanvas", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
        root.transform.SetParent(transform, false);
        _canvas = root.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 150;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var pulse = MakeRect("ThreatPulse", root.transform, Vector2.zero, Vector2.zero, Vector2.zero);
        pulse.anchorMax = Vector2.one;
        pulse.offsetMin = pulse.offsetMax = Vector2.zero;
        _pulse = pulse.gameObject.AddComponent<UnityEngine.UI.Image>();
        _pulse.color = Color.clear;
        _pulse.raycastTarget = false;

        var warning = MakeRect("ThreatWarning", root.transform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(700f, 90f));
        _warning = AddGroup(warning);
        AddText(warning, "They see you", 44f, TextAlignmentOptions.Center);
        var line = MakeRect("RedUnderline", warning, new Vector2(0.5f, 0f), new Vector2(0f, 5f), new Vector2(220f, 2f));
        var image = line.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = _accent;
        image.raycastTarget = false;

        var cooldown = MakeRect("TorchCooldown", root.transform, new Vector2(1f, 0f), new Vector2(-36f, 32f), new Vector2(390f, 64f));
        _cooldown = AddGroup(cooldown);
        var background = cooldown.gameObject.AddComponent<UnityEngine.UI.Image>();
        background.color = new Color(0.02f, 0.01f, 0.01f, 0.8f);
        background.raycastTarget = false;
        _cooldownText = AddText(cooldown, "", 23f, TextAlignmentOptions.Center);
    }

    private static RectTransform MakeRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static CanvasGroup AddGroup(RectTransform rect)
    {
        var group = rect.gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        return group;
    }

    private TextMeshProUGUI AddText(RectTransform parent, string text, float size, TextAlignmentOptions alignment)
    {
        var rect = MakeRect("Label", parent, Vector2.zero, Vector2.zero, Vector2.zero);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = _font != null ? _font : TMP_Settings.defaultFontAsset;
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = new Color(1f, 0.85f, 0.78f);
        label.raycastTarget = false;
        return label;
    }

    private void OnDisable()
    {
        if (_instance != this) return;
        _pursuitIntensity = 0f;
        if (_shake != null) _shake.SetProximityStrength(0f);
        ClearThreat();
        if (_villageMusicRoutine != null) StopCoroutine(_villageMusicRoutine);
        _villageMusicRoutine = null;
        if (_villageAudio != null && _villageAudio.BgmRevision == _villageMusicRevision && _villageAudio.HasBGM(_outsidePlaylist))
            _villageAudio.FadeInBGM(_outsidePlaylist);
        _villageMusicRevision = -1;
        _wasInVillage = null;
        _hasVillageMusicOverride = false;
        _cooldownTween?.Kill();
        if (_canvas != null) _canvas.enabled = false;
    }

    private void OnEnable()
    {
        if (_canvas != null) _canvas.enabled = true;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
        if (_canvas != null) Destroy(_canvas.gameObject);
    }
}
