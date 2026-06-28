using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Erumperem.Characters;
using Services.DebugUtilities;
using Services.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// ── DTO de snapshot ───────────────────────────────────────────────────────────

[Serializable]
public sealed class PlayableCharacterSnapshot
{
    public string CharacterName;
    public Vector3 Position;
    public Quaternion Rotation;
    public PlayableCharacterState State;
    public float CurrentHealth;

    public PlayableCharacterSnapshot(
        string name, Vector3 pos, Quaternion rot,
        PlayableCharacterState state,
        float currentHealth)
    {
        CharacterName = name;
        Position = pos;
        Rotation = rot;
        State = state;
        CurrentHealth = currentHealth;
    }
}

// ── Wrapper para serialização JSON ───────────────────────────────────────────

[Serializable]
internal sealed class SnapshotSaveData
{
    public List<PlayableCharacterSnapshot> Snapshots = new();
    public float CorruptionValue;
}

/// <summary>HP de um aliado: atual vem do save; máximo vem do catálogo de stats.</summary>
public readonly struct AllyHealthState
{
    public readonly float CurrentHealth;
    public readonly float MaxHealth;

    public AllyHealthState(float currentHealth, float maxHealth)
    {
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
    }
}

// ── Configuração de estado padrão ────────────────────────────────────────────

[Serializable]
public struct DefaultCharacterSetup
{
    public PlayableCharacter Character;
    public PlayableCharacterState InitialState;

    [Tooltip("HP máximo inicial do personagem.")]
    [Min(1f)]
    public int MaxHealth => Character.definition.MaxHitPoints;

    [Tooltip("HP corrente inicial no reset. Zero = começa com vida cheia.")]
    [Min(0f)]
    public float StartingCurrentHealth;
}

// ── ExplorationLoadContext ────────────────────────────────────────────────────

/// <summary>
/// Ponto central de save/load da exploração.
/// Orquestra personagens (<see cref="PlayableCharactersManager"/>) e
/// corrupção (<see cref="ExplorationCorruptionSystem"/>) num único ciclo.
/// </summary>
public sealed class ExplorationLoadContext : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [SerializeField] private string _explorationSceneName = "Overworld";

    [Tooltip("Estado padrão de cada personagem quando não há save.")]
    [SerializeField] private List<DefaultCharacterSetup> _defaultSetups = new();

    [Tooltip("Estado inicial de exploração por aliado (fallback quando não há DefaultCharacterSetup).")]
    [SerializeField] private AllyCharacterStatCatalog _allyCharacterStatCatalog;

    [Header("Sistemas")]
    [Tooltip("Referência ao ExplorationCorruptionSystem da cena. " +
             "Se nulo, a corrupção é ignorada no ciclo de save/load.")]
    [SerializeField] private ExplorationCorruptionSystem _corruptionSystem;

    [Header("IO Settings")]
    [SerializeField] private string _saveFileName = "exploration_save.json";
    [SerializeField] private string _saveFolderName = "Saves";

    [Header("Reset")]
    [Tooltip("Spawn do Wulfric ao fazer reset. Se vazio, procura 'ResetWulfricPosition' na cena.")]
    [SerializeField] private Transform _wulfricResetSpawn;

    // ── Singleton ─────────────────────────────────────────────────────────

    public static ExplorationLoadContext Instance { get; private set; }

    /// <summary>Disparado após snapshots ou defaults serem aplicados aos personagens da cena.</summary>
    public static event Action OnExplorationStateApplied;

    /// <summary>
    /// Verdadeiro enquanto <see cref="ApplySnapshots"/> ou <see cref="ApplyDefaultSetups"/>
    /// reposicionam personagens. Usado para suprimir cura da vila por triggers físicos durante o load.
    /// </summary>
    public static bool IsApplyingSavedExplorationState { get; private set; }

    // ── Serviço de IO ─────────────────────────────────────────────────────

    private readonly IFileService _fileService = new FileService();

    // ── Estado interno ────────────────────────────────────────────────────

    private List<PlayableCharacterSnapshot> _snapshots = new();
    private bool _hasSave;
    private bool _preferInMemorySnapshotsOnNextRestore;
    private bool _restoreStateInProgress;
    private string _saveDirectory;
    private float _savedCorruptionValue;
    private float _corruptionAtCombatEntry;
    private readonly Dictionary<string, float> _allyCurrentHealthAtCombatEntry =
        new(StringComparer.OrdinalIgnoreCase);

    private PlayableCharactersManager _manager;

    private readonly Dictionary<string, VillageSpawnSnapshot> _villageSpawnByCharacterName =
        new(StringComparer.OrdinalIgnoreCase);

    [Serializable]
    private struct VillageSpawnSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

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

        if (GetComponent<CombatExplorationBridge>() == null)
        {
            gameObject.AddComponent<CombatExplorationBridge>();
        }
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Start()
    {
        if (IsConfiguredExplorationScene(SceneManager.GetActiveScene()))
        {
            CombatExplorationBridge.Instance?.BlockExplorationCombatContactsAfterSceneLoad();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsConfiguredExplorationScene(scene))
        {
            return;
        }

        _manager = null;
        CombatExplorationBridge.Instance?.BlockExplorationCombatContactsAfterSceneLoad();
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

        if (sceneInstance._allyCharacterStatCatalog != null)
        {
            _allyCharacterStatCatalog = sceneInstance._allyCharacterStatCatalog;
        }

        if (sceneInstance._corruptionSystem != null)
        {
            _corruptionSystem = sceneInstance._corruptionSystem;
        }
        else
        {
            TryResolveCorruptionSystemFromScene();
        }

        if (sceneInstance._wulfricResetSpawn != null)
        {
            _wulfricResetSpawn = sceneInstance._wulfricResetSpawn;
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
            if (character == null)
            {
                continue;
            }

            var healthBar = character.HealthBar;
            if (healthBar == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    $"[SAVE] '{character.CharacterName}' não possui HealthBar — HP ignorado.",
                    LogCategory.Player);
            }

            var maxHealth = ResolveAllyMaxHealth(character.CharacterName);
            var currentHealth = healthBar != null
                ? Mathf.Clamp(healthBar.CurrentHealth, 0f, maxHealth)
                : maxHealth;

            _snapshots.Add(new PlayableCharacterSnapshot(
                character.CharacterName,
                character.Transform.position,
                character.Transform.rotation,
                character.CurrentState,
                currentHealth));
        }

        _savedCorruptionValue = ResolveCurrentCorruptionValue();
        _hasSave = _snapshots.Count > 0;
        if (_hasSave)
            await SaveToFileAsync();

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[SAVE] {_snapshots.Count} personagens salvos.", LogCategory.Player);

        // ── Corrupção ────────────────────────────────────────────────────
        if (_corruptionSystem != null)
        {
            _corruptionSystem.Corruption = _savedCorruptionValue;
            _corruptionSystem.SaveState();
        }
        else
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[SAVE] Corrupção ({_savedCorruptionValue:F1}) persistida no save de exploração.",
                LogCategory.Player);
    }

    /// <summary>
    /// Captura HP/corrupção de exploração imediatamente antes do combate.
    /// Usado para impedir que o retorno pós-combate aumente HP ou reduza corrupção.
    /// </summary>
    public void RememberExplorationStateAtCombatEntry()
    {
        _allyCurrentHealthAtCombatEntry.Clear();
        foreach (var snapshot in _snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.CharacterName))
            {
                continue;
            }

            _allyCurrentHealthAtCombatEntry[snapshot.CharacterName] = snapshot.CurrentHealth;
        }

        _corruptionAtCombatEntry = _savedCorruptionValue;

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[HEAL-DEBUG] [COMBAT-ENTRY] Baseline HP/corrupção memorizado " +
            $"({_allyCurrentHealthAtCombatEntry.Count} personagens, corrupção {_corruptionAtCombatEntry:F1}).",
            LogCategory.Player);
    }

    /// <summary>
    /// Carrega o save do disco (se necessário) e restaura personagens e corrupção.
    /// Aguarda explicitamente o IO da corrupção antes de aplicar, garantindo
    /// que o valor correto esteja disponível independente da ordem de Start().
    /// </summary>
    public async void RestoreState() => await RestoreStateAsync();

    /// <summary>Apaga o save e aplica posições/estado padrão da cena.</summary>
    public async Task ResetToDefaultStateAsync()
    {
        if (!TryGetManager())
        {
            return;
        }

        await ClearSaveAsync();
        CacheVillageSpawnPointsFromActiveScene();
        TryResolveCorruptionSystemFromScene();
        ApplyDefaultSetups();

        if (_corruptionSystem != null)
        {
            _corruptionSystem.RestoreState();
        }
    }

    public async Task RestoreStateAsync()
    {
        if (!TryGetManager()) return;

        if (_restoreStateInProgress)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[LOAD] RestoreStateAsync ignorado — restauração já em curso.",
                LogCategory.Player);
            return;
        }

        _restoreStateInProgress = true;
        try
        {
            TryResolveCorruptionSystemFromScene();

            var restoreSnapshotsFromMemory = _preferInMemorySnapshotsOnNextRestore;
            if (restoreSnapshotsFromMemory)
            {
                _preferInMemorySnapshotsOnNextRestore = false;
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[LOAD] Restaurando snapshots em memória (retorno de combate).",
                    LogCategory.Player);
            }
            else
            {
                await LoadFromFileAsync();
            }

            bool shouldApplySavedSnapshots = _hasSave && _snapshots.Count > 0;
            if (shouldApplySavedSnapshots)
            {
                ApplySnapshots();
                PersistCorruptionToDedicatedSaveFile();
            }
            else
            {
                if (_corruptionSystem != null)
                {
                    await _corruptionSystem.LoadAsync();
                    _corruptionSystem.RestoreState();
                    _savedCorruptionValue = _corruptionSystem.Corruption;
                }

                CacheVillageSpawnPointsFromActiveScene();
                ApplyDefaultSetups();
            }
        }
        finally
        {
            _restoreStateInProgress = false;
        }
    }

    /// <summary>
    /// Limpa o save em memória, remove os arquivos em disco e zera a corrupção (novo jogo).
    /// </summary>
    public async void ClearSave() => await ClearSaveAsync();

    public async Task ClearSaveAsync()
    {
        // ── Personagens ──────────────────────────────────────────────────
        _snapshots.Clear();
        _hasSave = false;
        _savedCorruptionValue = 0f;

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
            await _corruptionSystem.ClearSaveAsync();
    }

    public bool HasSave() => _hasSave;

    /// <summary>
    /// Garante um <see cref="Instance"/> (cria em runtime se necessário).
    /// Usado pela CombatScene ao dar play directo, sem passar pelo Overworld.
    /// </summary>
    public static ExplorationLoadContext EnsureRuntimeInstance(
        AllyCharacterStatCatalog allyCharacterStatCatalog = null)
    {
        if (Instance != null)
        {
            if (allyCharacterStatCatalog != null)
            {
                Instance.AssignAllyCharacterStatCatalog(allyCharacterStatCatalog);
            }

            return Instance;
        }

        var runtimeHost = new GameObject("[Runtime] ExplorationLoadContext");
        var runtimeContext = runtimeHost.AddComponent<ExplorationLoadContext>();

        if (allyCharacterStatCatalog != null)
        {
            runtimeContext.AssignAllyCharacterStatCatalog(allyCharacterStatCatalog);
        }

        Debug.Log("[Save] ExplorationLoadContext criado em runtime (play directo na CombatScene).");
        return Instance ?? runtimeContext;
    }

    public void AssignAllyCharacterStatCatalog(AllyCharacterStatCatalog allyCharacterStatCatalog)
    {
        if (allyCharacterStatCatalog != null)
        {
            _allyCharacterStatCatalog = allyCharacterStatCatalog;
        }
    }

    /// <summary>Carrega sempre o ficheiro de save do disco para memória (HP + corrupção).</summary>
    public async Task EnsureSaveLoadedFromDiskAsync()
    {
        EnsureSaveDirectoryInitialized();
        await LoadFromFileAsync();

        Debug.Log(
            $"[Save] Save lido do disco: {_snapshots.Count} snapshots, corrupção {_savedCorruptionValue:F1}, " +
            $"path={_saveDirectory}/{_saveFileName}");
    }

    private void EnsureSaveDirectoryInitialized()
    {
        if (string.IsNullOrEmpty(_saveDirectory))
        {
            _saveDirectory = System.IO.Path.Combine(Application.persistentDataPath, _saveFolderName);
        }
    }

    public IReadOnlyDictionary<string, AllyHealthState> GetSavedHealthByCharacterName()
    {
        var healthByCharacterName = new Dictionary<string, AllyHealthState>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in _snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.CharacterName))
            {
                continue;
            }

            healthByCharacterName[snapshot.CharacterName] = BuildAllyHealthState(
                snapshot.CharacterName,
                snapshot.CurrentHealth);
        }

        return healthByCharacterName;
    }

    public double GetSavedCorruptionValue()
    {
        if (_hasSave)
        {
            return _savedCorruptionValue;
        }

        if (_corruptionSystem != null)
        {
            return _corruptionSystem.Corruption;
        }

        return _savedCorruptionValue;
    }

    /// <summary>
    /// Party de combate [Main, Companion] a partir dos snapshots em memória (útil na cena de combate).
    /// </summary>
    public IReadOnlyList<string> GetCombatAllyCharacterNamesFromSnapshots()
    {
        return CombatPartyResolver.BuildPartyNamesFromSnapshots(_snapshots);
    }

    /// <summary>
    /// Atualiza snapshots em memória com HP/corrupção pós-combate.
    /// </summary>
    public void ApplyCombatHealthAndCorruptionToSnapshots(
        Game.Core.Models.BattleState battleState,
        IReadOnlyList<string> combatAllyCharacterNames,
        double corruptionValue,
        bool persistToDisk = true)
    {
        if (battleState == null)
        {
            return;
        }

        var allies = battleState.Allies;
        for (int allyIndex = 0; allyIndex < allies.Count && allyIndex < combatAllyCharacterNames.Count; allyIndex++)
        {
            var characterName = combatAllyCharacterNames[allyIndex];
            var snapshot = _snapshots.Find(savedSnapshot =>
                string.Equals(savedSnapshot.CharacterName, characterName, StringComparison.OrdinalIgnoreCase));

            if (snapshot == null)
            {
                continue;
            }

            var ally = allies[allyIndex];
            var maxHealth = ResolveAllyMaxHealth(characterName);
            snapshot.CurrentHealth = Mathf.Clamp(ally.Health.CurrentHp, 0f, maxHealth);

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[HEAL-DEBUG] [COMBAT-SAVE] '{characterName}' HP pós-combate salvo → " +
                $"{snapshot.CurrentHealth}/{maxHealth}.",
                LogCategory.Player);
        }

        ApplySavedCorruptionValue(corruptionValue);

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[HEAL-DEBUG] [COMBAT-SAVE] Corrupção pós-combate → {_savedCorruptionValue:F1}.",
            LogCategory.Player);

        _hasSave = _snapshots.Count > 0;
        if (_hasSave && persistToDisk)
        {
            _ = SaveToFileAsync();
        }

        PersistCorruptionToDedicatedSaveFile();
    }

    /// <summary>
    /// Reposiciona snapshots após derrota (retorno à aldeia).
    /// </summary>
    public void ApplyCombatDefeatReturnToVillage()
    {
        OverwriteSnapshotsForVillageReturn();
    }

    /// <summary>
    /// Atualiza snapshots em memória com HP/corrupção pós-combate e,
    /// em derrota, reposiciona o grupo na aldeia.
    /// </summary>
    public void ApplyCombatOutcomeToSnapshots(
        Game.Core.Models.BattleState battleState,
        IReadOnlyList<string> combatAllyCharacterNames,
        bool returnToVillage,
        bool persistToDisk = true)
    {
        ApplyCombatHealthAndCorruptionToSnapshots(
            battleState,
            combatAllyCharacterNames,
            battleState.CorruptionValue,
            persistToDisk);

        if (returnToVillage)
        {
            ApplyCombatDefeatReturnToVillage();
        }
    }

    public float ResolveAllyMaxHealth(string characterName)
    {
        if (_allyCharacterStatCatalog != null)
        {
            return _allyCharacterStatCatalog.GetExplorationMaxHealth(characterName);
        }

        return characterName switch
        {
            "Wulfric" => 100f,
            "Buck" => 200f,
            "Matsuda" => 100f,
            _ => 100f,
        };
    }

    private AllyHealthState BuildAllyHealthState(string characterName, float currentHealth)
    {
        var maxHealth = ResolveAllyMaxHealth(characterName);
        return new AllyHealthState(Mathf.Clamp(currentHealth, 0f, maxHealth), maxHealth);
    }

    private const float PartyWipeHealthThreshold = 1f;

    /// <summary>
    /// Verdadeiro quando Main e Companion têm HP estritamente abaixo de 1.
    /// </summary>
    public bool AreMainAndCompanionBelowOneHealth()
    {
        if (TryResolveMainAndCompanionCurrentHealthFromScene(
                out var mainCurrentHealth,
                out var companionCurrentHealth))
        {
            return mainCurrentHealth < PartyWipeHealthThreshold
                && companionCurrentHealth < PartyWipeHealthThreshold;
        }

        if (!TryResolveMainAndCompanionCurrentHealthFromSnapshots(
                out mainCurrentHealth,
                out companionCurrentHealth))
        {
            return false;
        }

        return mainCurrentHealth < PartyWipeHealthThreshold
            && companionCurrentHealth < PartyWipeHealthThreshold;
    }

    /// <summary>
    /// Se Main e Companion estão abaixo de 1 HP, apaga o save (retorno à vila / wipe).
    /// </summary>
    public async Task<bool> TryResetSaveIfMainAndCompanionAreDefeatedAsync()
    {
        if (!AreMainAndCompanionBelowOneHealth())
        {
            return false;
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[SAVE] Main e Companion com HP < 1 — save resetado (retorno à vila).",
            LogCategory.Player);

        await ClearSaveAsync();
        _snapshots.Clear();
        _hasSave = false;
        _savedCorruptionValue = 0f;
        return true;
    }

    /// <summary>
    /// Na vila, com manager disponível: reset completo + defaults se a party estiver derrotada.
    /// </summary>
    public async Task<bool> TryResetSaveAndApplyDefaultsIfMainAndCompanionAreDefeatedAsync()
    {
        if (!AreMainAndCompanionBelowOneHealth())
        {
            return false;
        }

        if (!TryGetManager())
        {
            return await TryResetSaveIfMainAndCompanionAreDefeatedAsync();
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[SAVE] Main e Companion com HP < 1 na vila — reset ao estado padrão.",
            LogCategory.Player);

        await ResetToDefaultStateAsync();
        return true;
    }

    private bool TryResolveMainAndCompanionCurrentHealthFromScene(
        out float mainCurrentHealth,
        out float companionCurrentHealth)
    {
        mainCurrentHealth = 0f;
        companionCurrentHealth = 0f;

        if (!TryGetManager())
        {
            return false;
        }

        if (_manager.Main is not PlayableCharacter mainPlayableCharacter
            || _manager.Companion is not PlayableCharacter companionPlayableCharacter)
        {
            return false;
        }

        if (mainPlayableCharacter.HealthBar == null || companionPlayableCharacter.HealthBar == null)
        {
            return false;
        }

        mainCurrentHealth = mainPlayableCharacter.HealthBar.CurrentHealth;
        companionCurrentHealth = companionPlayableCharacter.HealthBar.CurrentHealth;
        return true;
    }

    private bool TryResolveMainAndCompanionCurrentHealthFromSnapshots(
        out float mainCurrentHealth,
        out float companionCurrentHealth)
    {
        mainCurrentHealth = 0f;
        companionCurrentHealth = 0f;

        string mainCharacterName = null;
        string companionCharacterName = null;
        var hasMainHealthFromState = false;
        var hasCompanionHealthFromState = false;

        foreach (var snapshot in _snapshots)
        {
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.CharacterName))
            {
                continue;
            }

            if (snapshot.State == PlayableCharacterState.Main)
            {
                mainCharacterName = snapshot.CharacterName;
                mainCurrentHealth = snapshot.CurrentHealth;
                hasMainHealthFromState = true;
            }
            else if (snapshot.State == PlayableCharacterState.Companion)
            {
                companionCharacterName = snapshot.CharacterName;
                companionCurrentHealth = snapshot.CurrentHealth;
                hasCompanionHealthFromState = true;
            }
        }

        var partyFromSnapshots = CombatPartyResolver.BuildPartyNamesFromSnapshots(_snapshots);
        if (partyFromSnapshots.Count > 0)
        {
            mainCharacterName ??= partyFromSnapshots[0];
        }

        if (partyFromSnapshots.Count > 1)
        {
            companionCharacterName ??= partyFromSnapshots[1];
        }

        if (string.IsNullOrWhiteSpace(mainCharacterName) || string.IsNullOrWhiteSpace(companionCharacterName))
        {
            return false;
        }

        if (!hasMainHealthFromState
            && !TryGetSnapshotCurrentHealth(mainCharacterName, out mainCurrentHealth))
        {
            return false;
        }

        if (!hasCompanionHealthFromState
            && !TryGetSnapshotCurrentHealth(companionCharacterName, out companionCurrentHealth))
        {
            return false;
        }

        return true;
    }

    private bool TryGetSnapshotCurrentHealth(string characterName, out float currentHealth)
    {
        currentHealth = 0f;
        var snapshot = _snapshots.Find(savedSnapshot =>
            string.Equals(savedSnapshot.CharacterName, characterName, StringComparison.OrdinalIgnoreCase));

        if (snapshot == null)
        {
            return false;
        }

        currentHealth = snapshot.CurrentHealth;
        return true;
    }

    public void ApplySavedCorruptionValue(double corruptionValue)
    {
        var corruptionBefore = _savedCorruptionValue;
        _savedCorruptionValue = (float)Math.Max(0, corruptionValue);
        TryResolveCorruptionSystemFromScene();

        if (_savedCorruptionValue < corruptionBefore - 0.01f)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[HEAL-DEBUG] [LOAD-CONTEXT] Corrupção salva reduzida {corruptionBefore:F1} → {_savedCorruptionValue:F1}.",
                LogCategory.Player);
        }

        if (_corruptionSystem != null)
        {
            _corruptionSystem.Corruption = Mathf.Clamp(_savedCorruptionValue, 0f, _corruptionSystem.MaxCorruption);
        }
    }

    public void PersistCorruptionToDedicatedSaveFile()
    {
        TryResolveCorruptionSystemFromScene();
        if (_corruptionSystem == null) return;

        _corruptionSystem.Corruption = Mathf.Clamp(_savedCorruptionValue, 0f, _corruptionSystem.MaxCorruption);
        _corruptionSystem.SaveState();
    }

    /// <summary>
    /// Grava snapshots em memória e carrega a cena de exploração após retorno de combate.
    /// Evita race: nudge pós-vitória deve ser persistido antes do reload.
    /// </summary>
    public void FinishCombatReturnAndLoadExploration(
        string sceneName,
        bool checkPartyWipeSaveResetAtVillage = false)
    {
        StartCoroutine(FinishCombatReturnAndLoadExplorationRoutine(sceneName, checkPartyWipeSaveResetAtVillage));
    }

    // ── IO (personagens) ──────────────────────────────────────────────────

    private async System.Threading.Tasks.Task SaveToFileAsync()
    {
        try
        {
            var saveData = new SnapshotSaveData
            {
                Snapshots = _snapshots,
                CorruptionValue = _savedCorruptionValue,
            };
            string json = JsonUtility.ToJson(saveData, prettyPrint: true);

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
                _snapshots.Clear();
                _hasSave = false;
                _savedCorruptionValue = 0f;
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[LOAD] Nenhum arquivo de save encontrado.", LogCategory.Player);
                return;
            }

            FileData fileData = await _fileService.ReadAsync(_saveFileName, _saveDirectory);
            var saveData = JsonUtility.FromJson<SnapshotSaveData>(fileData._fileContent);

            if (saveData?.Snapshots != null && saveData.Snapshots.Count > 0)
            {
                _snapshots = saveData.Snapshots;
                _savedCorruptionValue = saveData.CorruptionValue;
                _hasSave = true;

                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[LOAD] {_snapshots.Count} snapshots carregados de '{fileData.FullPath}'.",
                    LogCategory.Player);
            }
            else
            {
                _snapshots.Clear();
                _hasSave = false;
                _savedCorruptionValue = 0f;
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
        CacheVillageSpawnPointsFromActiveScene();
        TryRestoreOnSceneReady();
    }

    private void TryRestoreOnSceneReady()
    {
        TryResolveCorruptionSystemFromScene();
        CacheVillageSpawnPointsFromActiveScene();
        if (!TryGetManager()) return;
        RestoreState();
    }

    private IEnumerator FinishCombatReturnAndLoadExplorationRoutine(
        string sceneName,
        bool checkPartyWipeSaveResetAtVillage)
    {
        if (checkPartyWipeSaveResetAtVillage)
        {
            var resetSaveTask = TryResetSaveIfMainAndCompanionAreDefeatedAsync();
            while (!resetSaveTask.IsCompleted)
            {
                yield return null;
            }
        }

        _preferInMemorySnapshotsOnNextRestore = _hasSave;
        _hasSave = _snapshots.Count > 0;

        PersistCorruptionToDedicatedSaveFile();

        if (_hasSave)
        {
            var saveTask = SaveToFileAsync();
            while (!saveTask.IsCompleted)
            {
                yield return null;
            }
        }

        if (ScenesManager.Instance == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[LOAD] ScenesManager ausente — retorno de combate abortado.",
                LogCategory.Player);
            yield break;
        }

        ScenesManager.Instance.LoadSceneByName(sceneName);
    }

    public bool TryGetSavedPositionForCharacter(string characterName, out Vector3 position)
    {
        var snapshot = _snapshots.Find(savedSnapshot =>
            string.Equals(savedSnapshot.CharacterName, characterName, StringComparison.Ordinal));

        if (snapshot == null)
        {
            position = default;
            return false;
        }

        position = snapshot.Position;
        return true;
    }

    /// <summary>
    /// Evita respawn em cima do gatilho de combate após vitória (ex.: fantasma estático da vila).
    /// </summary>
    public void NudgeSnapshotsAwayFromWorldPoint(Vector3 worldPoint, float minimumSeparationDistance)
    {
        if (minimumSeparationDistance <= 0f)
        {
            return;
        }

        var flatCombatEntry = new Vector3(worldPoint.x, 0f, worldPoint.z);
        foreach (var snapshot in _snapshots)
        {
            var flatSnapshotPosition = new Vector3(snapshot.Position.x, 0f, snapshot.Position.z);
            var offsetFromCombatEntry = flatSnapshotPosition - flatCombatEntry;
            if (offsetFromCombatEntry.sqrMagnitude >= minimumSeparationDistance * minimumSeparationDistance)
            {
                continue;
            }

            var pushDirection = offsetFromCombatEntry.sqrMagnitude > 0.0001f
                ? offsetFromCombatEntry.normalized
                : Vector3.forward;

            var nudgedPosition = flatCombatEntry + pushDirection * minimumSeparationDistance;
            nudgedPosition.y = snapshot.Position.y;
            snapshot.Position = nudgedPosition;
        }
    }

    /// <summary>
    /// Reposiciona o grupo após combate com inimigo estático do spawn:
    /// Wulfric no reset da vila; restantes nos Resting Points conhecidos.
    /// </summary>
    public void ReturnSnapshotsToResetSpawn()
    {
        var wulfricResetSpawnTransform = ResolveWulfricResetSpawnTransform();

        foreach (var snapshot in _snapshots)
        {
            if (string.Equals(snapshot.CharacterName, "Wulfric", StringComparison.Ordinal)
                && wulfricResetSpawnTransform != null)
            {
                snapshot.Position = wulfricResetSpawnTransform.position;
                snapshot.Rotation = wulfricResetSpawnTransform.rotation;
            }
            else if (_villageSpawnByCharacterName.TryGetValue(snapshot.CharacterName, out var villageSpawn))
            {
                snapshot.Position = villageSpawn.Position;
                snapshot.Rotation = villageSpawn.Rotation;
            }

            snapshot.State = ResolveDefaultExplorationState(snapshot.CharacterName);
        }
    }

    /// <summary>Snapshots em memória (elementos mutáveis).</summary>
    public IReadOnlyList<PlayableCharacterSnapshot> Snapshots => _snapshots;

    /// <summary>
    /// Atualiza posição/rotação de cada snapshot para o RestingPoint do personagem na cena.
    /// </summary>
    /// <returns>Número de snapshots actualizados.</returns>
    public int MoveSnapshotsToCharacterRestingPoints()
    {
        if (!TryGetManager())
        {
            return 0;
        }

        var patchedSnapshotCount = 0;

        foreach (var snapshot in _snapshots)
        {
            var playableCharacter = FindPlayableCharacterByName(snapshot.CharacterName);
            if (playableCharacter == null)
            {
                continue;
            }

            var restingPointTransform = playableCharacter.RestingPoint;
            if (restingPointTransform == null)
            {
                continue;
            }

            var previousPosition = snapshot.Position;
            snapshot.Position = restingPointTransform.position;
            snapshot.Rotation = restingPointTransform.rotation;
            patchedSnapshotCount++;

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[RestingPointPatcher] '{snapshot.CharacterName}' {previousPosition} → {snapshot.Position} (RestingPoint)",
                LogCategory.Player);
        }

        return patchedSnapshotCount;
    }

    /// <summary>
    /// Aplica snapshots já em memória na cena e grava em disco, sem reler o arquivo.
    /// </summary>
    public async void ApplySnapshotsAndSave()
    {
        if (!TryGetManager())
        {
            return;
        }

        _hasSave = _snapshots.Count > 0;
        if (!_hasSave)
        {
            return;
        }

        ApplySnapshots();

        _savedCorruptionValue = ResolveCurrentCorruptionValue();
        await SaveToFileAsync();

        if (_corruptionSystem != null)
        {
            _corruptionSystem.Corruption = _savedCorruptionValue;
            _corruptionSystem.SaveState();
        }
    }

    private void CacheVillageSpawnPointsFromActiveScene()
    {
        _villageSpawnByCharacterName.Clear();

        var restingPointsRoot = GameObject.Find("Resting Points");
        if (restingPointsRoot == null)
        {
            return;
        }

        foreach (Transform restingPointTransform in restingPointsRoot.transform)
        {
            _villageSpawnByCharacterName[restingPointTransform.name] = new VillageSpawnSnapshot
            {
                Position = restingPointTransform.position,
                Rotation = restingPointTransform.rotation,
            };
        }
    }

    private void OverwriteSnapshotsForVillageReturn()
    {
        foreach (var snapshot in _snapshots)
        {
            if (_villageSpawnByCharacterName.TryGetValue(snapshot.CharacterName, out var villageSpawn))
            {
                snapshot.Position = villageSpawn.Position;
                snapshot.Rotation = villageSpawn.Rotation;
            }

            snapshot.State = ResolveDefaultExplorationState(snapshot.CharacterName);
        }
    }

    private float ResolveCurrentCorruptionValue()
    {
        if (_corruptionSystem != null)
        {
            return _corruptionSystem.Corruption;
        }

        return _savedCorruptionValue;
    }

    private void TryResolveCorruptionSystemFromScene()
    {
        if (_corruptionSystem != null)
        {
            return;
        }

        _corruptionSystem = FindFirstObjectByType<ExplorationCorruptionSystem>();
    }

    private void ApplySnapshots()
    {
        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[HEAL-DEBUG] [LOAD] ApplySnapshots: aplicando HP/corrupção SALVOS ({_snapshots.Count} snapshots, " +
            $"corrupção {_savedCorruptionValue:F1}). OnExplorationStateApplied será disparado ao final.",
            LogCategory.Player);

        IsApplyingSavedExplorationState = true;
        try
        {
            foreach (var character in _manager.Playables)
            {
                var snap = _snapshots.Find(snapshot =>
                    string.Equals(snapshot.CharacterName, character.CharacterName, StringComparison.OrdinalIgnoreCase));
                if (snap == null)
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[LOAD] Snapshot não encontrado para '{character.CharacterName}'.",
                        LogCategory.Player);
                    continue;
                }

                if (character.HealthBar != null)
                {
                    var maxHealth = ResolveAllyMaxHealth(character.CharacterName);
                    character.HealthBar.SetMaxHealth(maxHealth, keepRatio: false);
                    character.HealthBar.RestoreForInitialization(
                        Mathf.Clamp(snap.CurrentHealth, 0f, maxHealth));

                    LoggerService.PrintLogMessage(LogLevel.Debug,
                        $"[HEAL-DEBUG] [LOAD] '{character.CharacterName}' HP → {snap.CurrentHealth}/{maxHealth}",
                        LogCategory.Player);
                }

                character.Transform.SetPositionAndRotation(snap.Position, snap.Rotation);
                _manager.SetState(snap.State, character);

                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[LOAD] '{character.CharacterName}' → {snap.State} @ {snap.Position}",
                    LogCategory.Player);
            }

            ApplySavedCorruptionValue(_savedCorruptionValue);

            _manager.NotifyCurrentMainIfAny();
            NotifyExplorationStateApplied();
        }
        finally
        {
            IsApplyingSavedExplorationState = false;
        }
    }

    private void ApplyDefaultSetups()
    {
        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[HEAL-DEBUG] [LOAD] ApplyDefaultSetups: aplicando HP/estado PADRÃO (reset). _hasSave={_hasSave}, " +
            $"snapshots={_snapshots.Count}. Se isto ocorrer num retorno pós-combate com save, é um bug de ordem.",
            LogCategory.Player);

        IsApplyingSavedExplorationState = true;
        try
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

            ApplyDefaultSpawnPositions();

            var orderedDefaultSetups = new List<DefaultCharacterSetup>(_defaultSetups);
            orderedDefaultSetups.Sort((leftSetup, rightSetup) =>
                GetDefaultSetupApplyOrder(leftSetup.InitialState)
                    .CompareTo(GetDefaultSetupApplyOrder(rightSetup.InitialState)));

            foreach (var setup in orderedDefaultSetups)
            {
                if (setup.Character == null) continue;

                _manager.SetState(setup.InitialState, setup.Character);

                var healthBar = setup.Character.HealthBar;
                if (healthBar != null && setup.MaxHealth > 0f)
                {
                    healthBar.SetMaxHealth(setup.MaxHealth, keepRatio: false);

                    float startingHealth = setup.StartingCurrentHealth > 0f
                        ? Mathf.Clamp(setup.StartingCurrentHealth, 0f, setup.MaxHealth)
                        : setup.MaxHealth;

                    healthBar.RestoreForInitialization(startingHealth);

                    LoggerService.PrintLogMessage(LogLevel.Debug,
                        $"[HEAL-DEBUG] [LOAD-DEFAULT] '{setup.Character.CharacterName}' HP padrão → {startingHealth}/{setup.MaxHealth}",
                        LogCategory.Player);
                }

                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[LOAD] '{setup.Character.CharacterName}' (padrão) → {setup.InitialState}",
                    LogCategory.Player);
            }

            _manager.NotifyCurrentMainIfAny();
            NotifyExplorationStateApplied();
        }
        finally
        {
            IsApplyingSavedExplorationState = false;
        }
    }

    private static void NotifyExplorationStateApplied() => OnExplorationStateApplied?.Invoke();

    private void ApplyDefaultSpawnPositions()
    {
        var wulfricResetSpawnTransform = ResolveWulfricResetSpawnTransform();

        foreach (var setup in _defaultSetups)
        {
            if (setup.Character == null)
            {
                continue;
            }

            if (string.Equals(setup.Character.CharacterName, "Wulfric", StringComparison.Ordinal)
                && wulfricResetSpawnTransform != null)
            {
                setup.Character.Transform.SetPositionAndRotation(
                    wulfricResetSpawnTransform.position,
                    wulfricResetSpawnTransform.rotation);
                continue;
            }

            var restingPoint = setup.Character.RestingPoint;
            if (restingPoint != null)
            {
                setup.Character.Transform.SetPositionAndRotation(
                    restingPoint.position,
                    restingPoint.rotation);
            }
        }
    }

    private Transform ResolveWulfricResetSpawnTransform()
    {
        if (_wulfricResetSpawn != null)
        {
            return _wulfricResetSpawn;
        }

        var resetSpawnObject = GameObject.Find("ResetWulfricPosition");
        return resetSpawnObject != null ? resetSpawnObject.transform : null;
    }

    private static int GetDefaultSetupApplyOrder(PlayableCharacterState initialState)
    {
        return initialState switch
        {
            PlayableCharacterState.Main => 0,
            PlayableCharacterState.Companion => 1,
            PlayableCharacterState.Resting => 2,
            _ => 3,
        };
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
                InitialState = ResolveDefaultExplorationState(playableCharacter.CharacterName),
            });
        }
    }

    private PlayableCharacterState ResolveDefaultExplorationState(string characterName)
    {
        if (_allyCharacterStatCatalog != null)
        {
            return _allyCharacterStatCatalog.GetDefaultExplorationState(characterName);
        }

        return characterName switch
        {
            "Wulfric" => PlayableCharacterState.Main,
            "Buck" => PlayableCharacterState.Companion,
            "Matsuda" => PlayableCharacterState.Resting,
            _ => PlayableCharacterState.Resting,
        };
    }

    private static float ResolveDefaultStartingHealth(string characterName)
    {
        return characterName switch
        {
            "Matsuda" => 0f,
            _ => 0f,
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

    private PlayableCharacter FindPlayableCharacterByName(string characterName)
    {
        foreach (var candidate in _manager.Playables)
        {
            if (candidate == null)
            {
                continue;
            }

            if (string.Equals(candidate.CharacterName, characterName, StringComparison.Ordinal))
            {
                return candidate;
            }
        }

        return null;
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

    /// <summary>
    /// Atualiza o HP atual de um aliado no save central (exploration_save.json).
    /// Usado em cenas sem PlayableCharactersManager (ex.: menu/loja).
    /// </summary>
    public async Task SaveAllyCurrentHealthAsync(string characterId, float currentHealth)
    {
        if (string.IsNullOrWhiteSpace(characterId))
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[SAVE-LIFE] CharacterId vazio — nada a salvar.", LogCategory.Player);
            return;
        }

        if (!_hasSave)
        {
            await LoadFromFileAsync();
        }

        var allyHealth = BuildAllyHealthState(characterId, currentHealth);

        var snapshot = _snapshots.Find(savedSnapshot =>
            string.Equals(savedSnapshot.CharacterName, characterId, StringComparison.OrdinalIgnoreCase));

        if (snapshot != null)
        {
            snapshot.CurrentHealth = allyHealth.CurrentHealth;
        }
        else
        {
            _snapshots.Add(new PlayableCharacterSnapshot(
                characterId,
                Vector3.zero,
                Quaternion.identity,
                ResolveDefaultExplorationState(characterId),
                allyHealth.CurrentHealth));
        }

        _hasSave = _snapshots.Count > 0;

        await SaveToFileAsync();

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[SAVE-LIFE] '{characterId}' HP atual → {allyHealth.CurrentHealth}/{allyHealth.MaxHealth}.",
            LogCategory.Player);
    }

    /// <summary>Wrapper síncrono (fire-and-forget) para chamar de UI/eventos sem await.</summary>
    public async void SaveAllyCurrentHealth(string characterId, float currentHealth) =>
        await SaveAllyCurrentHealthAsync(characterId, currentHealth);
}