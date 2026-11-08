using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class ExplorationAmbientEmitter : MonoBehaviour
{
    [SerializeField] private string _ambienceName = "ExplorationOneShots";
    [SerializeField] private Vector2 _intervalSeconds = new Vector2(8f, 20f);
    [SerializeField] private Vector2 _spawnDistance = new Vector2(10f, 18f);
    [SerializeField, Min(0f)] private float _heightOffset = 1.5f;
    [SerializeField, Min(1f)] private float _audibleDistance = 45f;

    private AudioSource _source;
    private AudioManager _audio;
    private PlayableCharactersManager _legacyCharacters;
    private PlayableCharacterController _characters;
    private VillageArea _village;
    private Hub _hub;
    private bool _combatScene;
    private bool _paused;
    private bool _wasExploring;
    private float _remaining;

    private void Awake()
    {
        _audio = AudioManager.instance != null ? AudioManager.instance : GetComponent<AudioManager>();
        var emitter = new GameObject("ExplorationAmbientPoint");
        emitter.transform.SetParent(transform, false);
        _source = emitter.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 1f;
        _source.spread = 0f;
        _source.dopplerLevel = 0f;
        _source.rolloffMode = AudioRolloffMode.Linear;
        _source.minDistance = 5f;
        ResetInterval();
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

    private void Start()
    {
        if (_audio == null) _audio = AudioManager.instance;
        ResolveScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _source.Stop();
        _paused = false;
        _wasExploring = false;
        ResolveScene();
        ResetInterval();
    }

    private void ResolveScene()
    {
        _legacyCharacters = FindFirstObjectByType<PlayableCharactersManager>();
        _characters = FindFirstObjectByType<PlayableCharacterController>();
        _village = FindFirstObjectByType<VillageArea>();
        _hub = FindFirstObjectByType<Hub>();
        _combatScene = FindFirstObjectByType<Erumperem.Combat.CombatSessionHub>() != null;
    }

    private void Update()
    {
        var player = _characters != null && _characters.InGameCharacter != null
            ? _characters.InGameCharacter.transform : _legacyCharacters?.Main?.Transform;
        if (_combatScene || player == null || !player.gameObject.activeInHierarchy
            || player.gameObject.scene != SceneManager.GetActiveScene()
            || SceneTransitionHandler.IsTransitioning || ExplorationLoadContext.IsApplyingSavedExplorationState
            || (_characters != null && _characters.IsLoadingState)
            || (_village != null && _village.isActiveAndEnabled && _village.ContainsPosition(player.position))
            || (_hub != null && _hub.isActiveAndEnabled && _hub.Contains(player.position)))
        {
            if (_source.isPlaying || _paused) _source.Stop();
            _paused = false;
            _wasExploring = false;
            return;
        }

        if (!_wasExploring)
        {
            _wasExploring = true;
            ResetInterval();
        }

        if (Time.timeScale <= 0f)
        {
            if (_source.isPlaying)
            {
                _source.Pause();
                _paused = true;
            }
            return;
        }
        if (_paused)
        {
            _source.UnPause();
            _paused = false;
        }
        if (_source.isPlaying) return;
        _remaining -= Time.deltaTime;
        if (_remaining > 0f) return;
        ResetInterval();

        var camera = Camera.main;
        if (_audio == null || camera == null || !_audio.TryConfigureAmbientSource(_ambienceName, _source, true)) return;
        if (!_audio.TryRouteAmbientSource(_source)) return;
        _source.transform.position = ChoosePosition(player.position, camera);
        _source.maxDistance = Mathf.Max(_audibleDistance,
            Vector3.Distance(camera.transform.position, player.position) + Mathf.Max(_spawnDistance.x, _spawnDistance.y) + 15f);
        _source.Play();
    }

    private Vector3 ChoosePosition(Vector3 center, Camera camera)
    {
        float minimum = Mathf.Max(1f, Mathf.Min(_spawnDistance.x, _spawnDistance.y));
        float maximum = Mathf.Max(minimum, Mathf.Max(_spawnDistance.x, _spawnDistance.y));
        Vector3 best = center;
        float bestEdgeDistance = -1f;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(minimum, maximum);
            var point = center + new Vector3(Mathf.Cos(angle) * radius, _heightOffset, Mathf.Sin(angle) * radius);
            var viewport = camera.WorldToViewportPoint(point);
            if (viewport.z <= 0f || viewport.x < -0.03f || viewport.x > 1.03f || viewport.y < -0.03f || viewport.y > 1.03f)
                return point;
            float edgeDistance = Mathf.Max(Mathf.Abs(viewport.x - 0.5f), Mathf.Abs(viewport.y - 0.5f));
            if (edgeDistance <= bestEdgeDistance) continue;
            bestEdgeDistance = edgeDistance;
            best = point;
        }
        return best;
    }

    private void ResetInterval()
    {
        float minimum = Mathf.Max(0.5f, Mathf.Min(_intervalSeconds.x, _intervalSeconds.y));
        _remaining = Random.Range(minimum, Mathf.Max(minimum, Mathf.Max(_intervalSeconds.x, _intervalSeconds.y)));
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (_source != null) _source.Stop();
        _paused = false;
        _wasExploring = false;
        ResetInterval();
    }

    private void OnDestroy()
    {
        if (_source != null) Destroy(_source.gameObject);
    }
}
