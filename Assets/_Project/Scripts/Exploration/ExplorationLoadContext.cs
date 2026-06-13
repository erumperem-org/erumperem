using System;
using System.Collections;
using System.Collections.Generic;
using Services.DebugUtilities;
using Services.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// ── DTO de snapshot ───────────────────────────────────────────────────────────

[Serializable]
public sealed class PlayableCharacterSnapshot
{
    public string                 CharacterName;
    public Vector3                Position;
    public Quaternion             Rotation;
    public PlayableCharacterState State;
    public float                  CurrentHealth;
    public float                  MaxHealth;

    public PlayableCharacterSnapshot(
        string name, Vector3 pos, Quaternion rot,
        PlayableCharacterState state,
        float currentHealth, float maxHealth)
    {
        CharacterName = name;
        Position      = pos;
        Rotation      = rot;
        State         = state;
        CurrentHealth = currentHealth;
        MaxHealth     = maxHealth;
    }
}

// ── Wrapper para serialização JSON ───────────────────────────────────────────

[Serializable]
internal sealed class SnapshotSaveData
{
    public List<PlayableCharacterSnapshot> Snapshots = new();
}

// ── Configuração de estado padrão ────────────────────────────────────────────

[Serializable]
public struct DefaultCharacterSetup
{
    public PlayableCharacter      Character;
    public PlayableCharacterState InitialState;

    [Tooltip("HP máximo inicial do personagem.")]
    [Min(1f)]
    public float MaxHealth;

    [Tooltip("HP corrente inicial. Se zero, iniciará com HP cheio.")]
    [Min(0f)]
    public float StartingHealth;
}

// ── ExplorationLoadContext ────────────────────────────────────────────────────

/// <summary>
/// Ponto central de save/load da exploração.
/// Orquestra personagens (<see cref="PlayableCharactersManager"/>) e
/// corrupção (<see cref="ExplorationCorruptionSystem"/>) num único ciclo.
///
/// MUDANÇAS vs versão anterior:
///   - Recebe referência opcional a <see cref="ExplorationCorruptionSystem"/>.
///   - <c>SaveState</c>   → também chama <c>corruptionSystem.SaveState()</c>.
///   - <c>RestoreState</c>→ também chama <c>corruptionSystem.RestoreState()</c>.
///   - <c>ClearSave</c>   → também chama <c>corruptionSystem.ClearSave()</c>.
///   - Corrupção é carregada/zerada de forma independente pelo próprio sistema;
///     o LoadContext apenas coordena o momento da chamada.
/// </summary>
public sealed class ExplorationLoadContext : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [SerializeField] private string _explorationSceneName = "Overworld";

    [Tooltip("Estado padrão de cada personagem quando não há save.")]
    [SerializeField] private List<DefaultCharacterSetup> _defaultSetups = new();

    [Header("Sistemas")]
    [Tooltip("Referência ao ExplorationCorruptionSystem da cena. " +
             "Se nulo, a corrupção é ignorada no ciclo de save/load.")]
    [SerializeField] private ExplorationCorruptionSystem _corruptionSystem;

    [Header("IO Settings")]
    [SerializeField] private string _saveFileName   = "exploration_save.json";
    [SerializeField] private string _saveFolderName = "Saves";

    // ── Singleton ─────────────────────────────────────────────────────────

    public static ExplorationLoadContext Instance { get; private set; }

    // ── Serviço de IO ─────────────────────────────────────────────────────

    private readonly IFileService _fileService = new FileService();

    // ── Estado interno ────────────────────────────────────────────────────

    private List<PlayableCharacterSnapshot> _snapshots = new();
    private bool   _hasSave;
    private string _saveDirectory;

    private PlayableCharactersManager _manager;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.AdoptSceneConfigurationFrom(this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _saveDirectory = System.IO.Path.Combine(Application.persistentDataPath, _saveFolderName);
    }

    private void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Start()
    {
        if (IsConfiguredExplorationScene(SceneManager.GetActiveScene()))
            TryRestoreOnSceneReady();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsConfiguredExplorationScene(scene))
        {
            return;
        }

        _manager = null;
        StartCoroutine(RestoreNextFrame());
    }

    private void AdoptSceneConfigurationFrom(ExplorationLoadContext sceneInstance)
    {
        if (sceneInstance == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(sceneInstance._explorationSceneName))
        {
            _explorationSceneName = sceneInstance._explorationSceneName;
        }

        if (sceneInstance._defaultSetups != null && sceneInstance._defaultSetups.Count > 0)
        {
            _defaultSetups = new List<DefaultCharacterSetup>(sceneInstance._defaultSetups);
        }

        if (sceneInstance._corruptionSystem != null)
        {
            _corruptionSystem = sceneInstance._corruptionSystem;
        }

        _manager = null;
    }

    private bool IsConfiguredExplorationScene(Scene scene)
    {
        if (!scene.IsValid())
        {
            return false;
        }

        return string.Equals(scene.name, _explorationSceneName, StringComparison.Ordinal)
            || (string.Equals(_explorationSceneName, "Overworld", StringComparison.Ordinal)
                && string.Equals(scene.name, "OverorldMerge", StringComparison.Ordinal));
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Salva posição, rotação, estado, HP de todos os personagens
    /// e o valor atual de corrupção.
    /// </summary>
    public async void SaveState()
    {
        if (!TryGetManager()) return;

        // ── Personagens ──────────────────────────────────────────────────
        _snapshots.Clear();
        foreach (var character in _manager.Playables)
        {
            if (character == null) continue;

            var hp = character.HealthBar;
            if (hp == null)
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    $"[SAVE] '{character.CharacterName}' não possui HealthBar — HP ignorado.",
                    LogCategory.Player);

            _snapshots.Add(new PlayableCharacterSnapshot(
                character.CharacterName,
                character.Transform.position,
                character.Transform.rotation,
                character.CurrentState,
                hp?.CurrentHealth ?? 0f,
                hp?.MaxHealth     ?? 0f));
        }

        _hasSave = _snapshots.Count > 0;
        if (_hasSave)
            await SaveToFileAsync();

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[SAVE] {_snapshots.Count} personagens salvos.", LogCategory.Player);

        // ── Corrupção ────────────────────────────────────────────────────
        if (_corruptionSystem != null)
            _corruptionSystem.SaveState();
        else
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[SAVE] ExplorationCorruptionSystem não atribuído — corrupção não salva.",
                LogCategory.Player);
    }

    /// <summary>
    /// Carrega o save do disco (se necessário) e restaura personagens e corrupção.
    /// Aguarda explicitamente o IO da corrupção antes de aplicar, garantindo
    /// que o valor correto esteja disponível independente da ordem de Start().
    /// </summary>
    public async void RestoreState()
    {
        if (!TryGetManager()) return;

        // ── Corrupção: lê o arquivo primeiro (awaited) ───────────────────
        if (_corruptionSystem != null)
            await _corruptionSystem.LoadAsync();

        // ── Personagens ──────────────────────────────────────────────────
        if (!_hasSave || _snapshots.Count == 0)
            await LoadFromFileAsync();

        if (_hasSave && _snapshots.Count > 0)
            ApplySnapshots();
        else
            ApplyDefaultSetups();

        // ── Corrupção: aplica o valor já carregado ───────────────────────
        if (_corruptionSystem != null)
            _corruptionSystem.RestoreState();
    }

    /// <summary>
    /// Limpa o save em memória, remove os arquivos em disco e zera a corrupção (novo jogo).
    /// </summary>
    public async void ClearSave()
    {
        // ── Personagens ──────────────────────────────────────────────────
        _snapshots.Clear();
        _hasSave = false;

        try
        {
            await _fileService.DeleteAsync(_saveFileName, _saveDirectory);
            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[SAVE] Arquivo de personagens deletado.", LogCategory.Player);
        }
        catch (Exception ex)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                $"[SAVE] Falha ao deletar arquivo de personagens: {ex.Message}", LogCategory.Player);
        }

        // ── Corrupção ────────────────────────────────────────────────────
        if (_corruptionSystem != null)
            _corruptionSystem.ClearSave();
    }

    public bool HasSave() => _hasSave;

    // ── IO (personagens) ──────────────────────────────────────────────────

    private async System.Threading.Tasks.Task SaveToFileAsync()
    {
        try
        {
            var saveData = new SnapshotSaveData { Snapshots = _snapshots };
            string json  = JsonUtility.ToJson(saveData, prettyPrint: true);

            var fileData = new FileData(json, _saveFileName, _saveDirectory);
            await _fileService.WriteAsync(fileData);

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SAVE] Arquivo gravado em: {fileData.FullPath}", LogCategory.Player);
        }
        catch (Exception ex)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[SAVE] Falha ao gravar save: {ex.Message}", LogCategory.Player);
        }
    }

    private async System.Threading.Tasks.Task LoadFromFileAsync()
    {
        try
        {
            bool exists = await _fileService.ExistsAsync(_saveFileName, _saveDirectory);
            if (!exists)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[LOAD] Nenhum arquivo de save encontrado.", LogCategory.Player);
                return;
            }

            FileData fileData = await _fileService.ReadAsync(_saveFileName, _saveDirectory);
            var saveData = JsonUtility.FromJson<SnapshotSaveData>(fileData._fileContent);

            if (saveData?.Snapshots != null && saveData.Snapshots.Count > 0)
            {
                _snapshots = saveData.Snapshots;
                _hasSave   = true;

                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[LOAD] {_snapshots.Count} snapshots carregados de '{fileData.FullPath}'.",
                    LogCategory.Player);
            }
        }
        catch (Exception ex)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[LOAD] Falha ao ler save: {ex.Message}", LogCategory.Player);
        }
    }

    // ── Restauração (personagens) ─────────────────────────────────────────

    private IEnumerator RestoreNextFrame()
    {
        yield return null;
        TryRestoreOnSceneReady();
    }

    private void TryRestoreOnSceneReady()
    {
        if (!TryGetManager()) return;
        RestoreState();
    }

    private void ApplySnapshots()
    {
        foreach (var character in _manager.Playables)
        {
            var snap = _snapshots.Find(s => s.CharacterName == character.CharacterName);
            if (snap == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    $"[LOAD] Snapshot não encontrado para '{character.CharacterName}'.",
                    LogCategory.Player);
                continue;
            }

            character.Transform.SetPositionAndRotation(snap.Position, snap.Rotation);
            _manager.SetState(snap.State, character);

            if (character.HealthBar != null && snap.MaxHealth > 0f)
            {
                character.HealthBar.SetMaxHealth(snap.MaxHealth, keepRatio: false);
                character.HealthBar.Kill();
                character.HealthBar.Heal(snap.CurrentHealth);

                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[LOAD] '{character.CharacterName}' HP → {snap.CurrentHealth}/{snap.MaxHealth}",
                    LogCategory.Player);
            }

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[LOAD] '{character.CharacterName}' → {snap.State} @ {snap.Position}",
                LogCategory.Player);
        }

        _hasSave = false;
    }

    private void ApplyDefaultSetups()
    {
        RemoveDestroyedDefaultSetups();

        if (_defaultSetups.Count == 0)
        {
            TryBuildFallbackDefaultSetups();
        }

        if (_defaultSetups.Count == 0)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[LOAD] Nenhum DefaultCharacterSetup configurado no Inspector.", LogCategory.Player);
            return;
        }

        foreach (var setup in _defaultSetups)
        {
            if (setup.Character == null) continue;

            _manager.SetState(setup.InitialState, setup.Character);

            var hp = setup.Character.HealthBar;
            if (hp != null && setup.MaxHealth > 0f)
            {
                hp.SetMaxHealth(setup.MaxHealth, keepRatio: false);

                float startHp = setup.StartingHealth > 0f
                    ? Mathf.Clamp(setup.StartingHealth, 0f, setup.MaxHealth)
                    : setup.MaxHealth;

                hp.Kill();
                hp.Heal(startHp);

                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[LOAD] '{setup.Character.CharacterName}' HP padrão → {startHp}/{setup.MaxHealth}",
                    LogCategory.Player);
            }

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[LOAD] '{setup.Character.CharacterName}' (padrão) → {setup.InitialState}",
                LogCategory.Player);
        }
    }

    private void TryBuildFallbackDefaultSetups()
    {
        if (!TryGetManager())
        {
            return;
        }

        foreach (var playableCharacter in _manager.Playables)
        {
            if (playableCharacter == null)
            {
                continue;
            }

            _defaultSetups.Add(new DefaultCharacterSetup
            {
                Character = playableCharacter,
                InitialState = ResolveFallbackInitialState(playableCharacter.CharacterName),
                MaxHealth = ResolveFallbackMaxHealth(playableCharacter.CharacterName),
                StartingHealth = 0f,
            });
        }
    }

    private static PlayableCharacterState ResolveFallbackInitialState(string characterName)
    {
        return characterName switch
        {
            "Wulfric" => PlayableCharacterState.Main,
            "Girl" => PlayableCharacterState.Companion,
            "Buck" => PlayableCharacterState.Resting,
            _ => PlayableCharacterState.Resting,
        };
    }

    private static float ResolveFallbackMaxHealth(string characterName)
    {
        return characterName switch
        {
            "Wulfric" => 100f,
            "Buck" => 200f,
            "Girl" => 30f,
            _ => 100f,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void RemoveDestroyedDefaultSetups()
    {
        for (int setupIndex = _defaultSetups.Count - 1; setupIndex >= 0; setupIndex--)
        {
            if (_defaultSetups[setupIndex].Character == null)
            {
                _defaultSetups.RemoveAt(setupIndex);
            }
        }
    }

    private bool TryGetManager()
    {
        if (_manager != null && !_manager.gameObject.scene.isLoaded)
        {
            _manager = null;
        }

        if (_manager == null)
        {
            _manager = FindFirstObjectByType<PlayableCharactersManager>();
        }

        if (_manager == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                "[LOAD] PlayableCharactersManager não encontrado na cena.", LogCategory.Player);
            return false;
        }

        return true;
    }
}