using Services.DebugUtilities;
using UnityEngine;

/// <summary>
/// Regenera a vida atual de todos os personagens gerenciados pelo
/// <see cref="PlayableCharactersManager"/> enquanto dentro da área segura (vila).
///
/// Padrão idêntico ao <see cref="ExplorationCorruptionSystem"/>:
///   - Usa um centro e raio para definir a área de regeneração.
///   - Dentro do raio: regenera HP a cada tick (<c>_healPerSecond</c>).
///   - Fora do raio: regeneração pausada.
///   - Itera <c>_manager.Playables</c> e cura via <see cref="PlayableHealthBar.Heal"/>.
/// </summary>
public sealed class VillageHealthRegenSystem : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Referências")]
    [SerializeField] private PlayableCharactersManager _manager;
    [SerializeField] private GameObject _safeAreaCenter;
    [SerializeField, Min(0.1f)] private float _safeAreaRadius = 10f;

    [Header("Regeneração")]
    [Tooltip("HP restaurado por segundo enquanto dentro da área segura.")]
    [SerializeField, Min(0f)] private float _healPerSecond = 5f;

    [Tooltip("Intervalo em segundos entre cada pulso de cura.")]
    [SerializeField, Min(0.1f)] private float _intervalSeconds = 1f;

    // ── Estado interno ────────────────────────────────────────────────────

    private IPlayableCharacter _main;
    private float _elapsed;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        if (_manager == null)
            _manager = FindFirstObjectByType<PlayableCharactersManager>();
    }

    private void OnEnable()
    {
        if (_manager != null)
            _manager.OnMainChanged += HandleMainChanged;
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.OnMainChanged -= HandleMainChanged;
    }

    private void Start()
    {
        if (_manager != null && _manager.Main != null)
            HandleMainChanged(_manager.Main);
    }

    private void Update()
    {
        if (_main == null || _safeAreaCenter == null) return;

        if (!IsInsideSafeArea()) return;

        _elapsed += Time.deltaTime;
        if (_elapsed < _intervalSeconds) return;

        _elapsed -= _intervalSeconds;
        HealAllPlayables();
    }

    // ── Handlers ─────────────────────────────────────────────────────────

    private void HandleMainChanged(IPlayableCharacter newMain) => _main = newMain;

    // ── Lógica ───────────────────────────────────────────────────────────

    private bool IsInsideSafeArea()
    {
        float dist = Vector3.Distance(_main.Transform.position, _safeAreaCenter.transform.position);
        return dist <= _safeAreaRadius;
    }

    private void HealAllPlayables()
    {
        int healAmount = Mathf.RoundToInt(_healPerSecond * _intervalSeconds);
        if (healAmount <= 0) return;

        foreach (var character in _manager.Playables)
        {
            if (character == null || character.HealthBar == null) continue;

            character.HealthBar.Heal(healAmount);

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[VillageRegen] [{character.CharacterName}] +{healAmount} HP.",
                LogCategory.Player);
        }
        FindAnyObjectByType<CharacterViewHud>().RefreshAll();
    }

    // ── Gizmos ────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_safeAreaCenter == null) return;
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.15f);
        Gizmos.DrawSphere(_safeAreaCenter.transform.position, _safeAreaRadius);
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
        Gizmos.DrawWireSphere(_safeAreaCenter.transform.position, _safeAreaRadius);
    }
#endif
}