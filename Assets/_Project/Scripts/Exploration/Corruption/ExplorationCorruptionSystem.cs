using System;
using System.Threading.Tasks;
using Services.DebugUtilities;
using Services.IO;
using UnityEngine;
using UnityEngine.UI;

public enum CorruptionTier { Low, Mid, High }

// ── DTO de save ───────────────────────────────────────────────────────────────

[Serializable]
internal sealed class CorruptionSaveData
{
    public float Corruption;
}

/// <summary>
/// Gerencia a corrupção do personagem principal baseada em tempo e distância
/// da área segura. Persiste e restaura via <see cref="IFileService"/>.
///
/// Ciclo de IO coordenado pelo <see cref="ExplorationLoadContext"/>:
///   LoadAsync()    → lê o arquivo e popula o valor interno (aguardável).
///   RestoreState() → aplica o valor carregado (ou default 0) na runtime.
///   SaveState()    → grava o valor atual em disco.
///   ClearSave()    → zera para 0 e deleta o arquivo.
/// </summary>
public sealed class ExplorationCorruptionSystem : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Referências")]
    [SerializeField] private PlayableCharactersManager _manager;
    [SerializeField] private GameObject _safeAreaCenter;
    [SerializeField, Min(0.1f)] private float _safeAreaRadius = 10f;

    [Header("Taxas")]
    [SerializeField, Min(0f)] private float _baseGainPerSecond = 2f;
    [SerializeField, Min(0f)] private float _gainPerMeterBeyondRadius = 0.5f;
    [SerializeField, Min(0f)] private float _decayPerSecond = .1f;

    [Header("UI")]
    [SerializeField] private Slider _corruptionSlider;
    [SerializeField] private TMPro.TMP_Text _corruptionNumber;

    [Header("IO Settings")]
    [Tooltip("Deve coincidir com a pasta usada pelo ExplorationLoadContext.")]
    [SerializeField] private string _saveFolderName = "Saves";
    [SerializeField] private string _saveFileName = "corruption_save.json";

    // ── Eventos públicos ──────────────────────────────────────────────────

    public event Action OnTierLow;
    public event Action OnTierMid;
    public event Action OnTierHigh;

    /// <summary>Disparado sempre que o valor de corrupção muda (0–250). A UI reage a este evento.</summary>
    public event Action<float> OnCorruptionChanged;

    // ── Estado interno ────────────────────────────────────────────────────

    private float _corruption;

    /// <summary>
    /// Valor atual de corrupção (0–250). O setter faz clamp e dispara
    /// <see cref="OnCorruptionChanged"/> apenas quando o valor muda.
    /// </summary>
    public float Corruption
    {
        get => _corruption;
        set
        {
            float clampedCorruption = Mathf.Clamp(value, 0f, 250f);
            if (Mathf.Approximately(_corruption, clampedCorruption))
            {
                return;
            }

            if (clampedCorruption < _corruption)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[HEAL-DEBUG] [CORRUPTION] Corrupção reduzida {_corruption:F1} → {clampedCorruption:F1}. " +
                    $"Origem: {ResolveCorruptionCallSiteDescription()}",
                    LogCategory.Player);
            }

            _corruption = clampedCorruption;
            OnCorruptionChanged?.Invoke(_corruption);
        }
    }

    public CorruptionTier CurrentTier { get; private set; } = CorruptionTier.Low;

    private IPlayableCharacter _main;
    private CorruptionTier _lastTier = CorruptionTier.Low;

    private readonly IFileService _fileService = new FileService();
    private string _saveDirectory;

    private float _loadedCorruption;
    private bool _loadCompleted;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        _saveDirectory = System.IO.Path.Combine(Application.persistentDataPath, _saveFolderName);
    }

    private void OnEnable()
    {
        if (_manager != null)
            _manager.OnMainChanged += HandleMainChanged;

        OnCorruptionChanged += HandleCorruptionChanged;
    }

    private void OnDisable()
    {
        if (_manager != null)
            _manager.OnMainChanged -= HandleMainChanged;

        OnCorruptionChanged -= HandleCorruptionChanged;
    }

    private void Start()
    {
        InitSlider();

        if (_manager != null && _manager.Main != null)
            HandleMainChanged(_manager.Main);
    }

    private void Update()
    {
        if (_main == null || _safeAreaCenter == null) return;

        UpdateCorruption();
        CheckTierTransition();
    }

    // ── API pública de IO ─────────────────────────────────────────────────

    public async Task LoadAsync()
    {
        await LoadFromFileAsync();
        _loadCompleted = true;
    }

    public void RestoreState()
    {
        float value = _loadCompleted ? _loadedCorruption : 0f;
        ApplyCorruption(value, fireEvents: false);

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[CORRUPTION] RestoreState: {Corruption:F1} (Tier: {CurrentTier})",
            LogCategory.Player);
    }

    public async void SaveState() => await SaveToFileAsync();

    public async void ClearSave() => await ClearSaveAsync();

    public async Task ClearSaveAsync()
    {
        _loadedCorruption = 0f;
        _loadCompleted = true;
        ApplyCorruption(0f, fireEvents: false);

        try
        {
            await _fileService.DeleteAsync(_saveFileName, _saveDirectory);
            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[CORRUPTION] Arquivo de save deletado.", LogCategory.Player);
        }
        catch (Exception ex)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                $"[CORRUPTION] Falha ao deletar save: {ex.Message}", LogCategory.Player);
        }
    }

    private static string ResolveCorruptionCallSiteDescription()
    {
        var stackTrace = new System.Diagnostics.StackTrace(false);
        for (int frameIndex = 0; frameIndex < stackTrace.FrameCount; frameIndex++)
        {
            var callingMethod = stackTrace.GetFrame(frameIndex)?.GetMethod();
            var declaringType = callingMethod?.DeclaringType;
            if (declaringType != null && declaringType != typeof(ExplorationCorruptionSystem))
            {
                return $"{declaringType.Name}.{callingMethod.Name}";
            }
        }

        return "desconhecido";
    }

    // ── Handlers ─────────────────────────────────────────────────────────

    private void HandleMainChanged(IPlayableCharacter newMain) => _main = newMain;

    private void HandleCorruptionChanged(float corruptionValue) => UpdateSlider();

    // ── Lógica de corrupção ───────────────────────────────────────────────

    private void UpdateCorruption()
    {
        float beyondRadius = DistanceBeyondRadius();

        if (beyondRadius <= 0f)
        {
            Corruption -= _decayPerSecond * Time.deltaTime;
            return;
        }

        float gainPerSecond = _baseGainPerSecond + _gainPerMeterBeyondRadius * beyondRadius;
        Corruption += gainPerSecond * Time.deltaTime;
    }

    private float DistanceBeyondRadius()
    {
        float dist = Vector3.Distance(_main.Transform.position, _safeAreaCenter.transform.position);
        return Mathf.Max(0f, dist - _safeAreaRadius);
    }

    // ── Faixas ────────────────────────────────────────────────────────────

    private void CheckTierTransition()
    {
        var tier = TierFor(Corruption);
        if (tier == _lastTier) return;

        _lastTier = CurrentTier = tier;
        switch (tier)
        {
            case CorruptionTier.Low:  OnTierLow?.Invoke();  break;
            case CorruptionTier.Mid:  OnTierMid?.Invoke();  break;
            case CorruptionTier.High: OnTierHigh?.Invoke(); break;
        }
    }

    private static CorruptionTier TierFor(float v) =>
        v < 125f ? CorruptionTier.Low : v < 187.5f ? CorruptionTier.Mid : CorruptionTier.High;

    // ── Helpers de aplicação ──────────────────────────────────────────────

    private void ApplyCorruption(float value, bool fireEvents)
    {
        Corruption = value;
        var tier = TierFor(Corruption);
        _lastTier = CurrentTier = tier;

        if (fireEvents)
            switch (tier)
            {
                case CorruptionTier.Low:  OnTierLow?.Invoke();  break;
                case CorruptionTier.Mid:  OnTierMid?.Invoke();  break;
                case CorruptionTier.High: OnTierHigh?.Invoke(); break;
            }

        UpdateSlider();
    }

    // ── IO ────────────────────────────────────────────────────────────────

    private async Task SaveToFileAsync()
    {
        try
        {
            var json = JsonUtility.ToJson(new CorruptionSaveData { Corruption = Corruption }, true);
            var fileData = new FileData(json, _saveFileName, _saveDirectory);
            await _fileService.WriteAsync(fileData);

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[CORRUPTION] Salvo: {Corruption:F1}", LogCategory.Player);
        }
        catch (Exception ex)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[CORRUPTION] Falha ao salvar: {ex.Message}", LogCategory.Player);
        }
    }

    private async Task LoadFromFileAsync()
    {
        try
        {
            if (!await _fileService.ExistsAsync(_saveFileName, _saveDirectory))
            {
                _loadedCorruption = 0f;
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[CORRUPTION] Sem arquivo de save — default 0.", LogCategory.Player);
                return;
            }

            var fileData = await _fileService.ReadAsync(_saveFileName, _saveDirectory);
            var data = JsonUtility.FromJson<CorruptionSaveData>(fileData._fileContent);

            _loadedCorruption = data != null ? Mathf.Clamp(data.Corruption, 0f, 250f) : 0f;

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[CORRUPTION] Lido do disco: {_loadedCorruption:F1}", LogCategory.Player);
        }
        catch (Exception ex)
        {
            _loadedCorruption = 0f;
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[CORRUPTION] Falha ao ler (default 0): {ex.Message}", LogCategory.Player);
        }
    }

    // ── UI ────────────────────────────────────────────────────────────────

    private void InitSlider()
    {
        if (_corruptionSlider == null) return;
        _corruptionSlider.minValue = 0f;
        _corruptionSlider.maxValue = 1f;
        UpdateSlider();
    }

    private void UpdateSlider()
    {
        if (_corruptionSlider == null) return;
        _corruptionSlider.value = Corruption / 250f;
        if (_corruptionNumber != null)
            _corruptionNumber.text = Mathf.RoundToInt(Corruption / 250f * 100f).ToString();
    }

    // ── Gizmos ────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (_safeAreaCenter == null) return;
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
        Gizmos.DrawSphere(_safeAreaCenter.transform.position, _safeAreaRadius);
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireSphere(_safeAreaCenter.transform.position, _safeAreaRadius);
    }
#endif
}