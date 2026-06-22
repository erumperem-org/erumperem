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
    public float CorruptionValue;
}

[Serializable]
public readonly struct ExplorationHealthSnapshot
{
    public readonly float CurrentHealth;
    public readonly float MaxHealth;

    public ExplorationHealthSnapshot(float currentHealth, float maxHealth)
    {
        CurrentHealth = currentHealth;
        MaxHealth     = maxHealth;
    }
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

    [Tooltip("Estado inicial de exploração por aliado (fallback quando não há DefaultCharacterSetup).")]
    [SerializeField] private AllyCharacterStatCatalog _allyCharacterStatCatalog;

    [Header("Sistemas")]
    [Tooltip("Referência ao ExplorationCorruptionSystem da cena. " +
             "Se nulo, a corrupção é ignorada no ciclo de save/load.")]
    [SerializeField] private ExplorationCorruptionSystem _corruptionSystem;

    [Header("IO Settings")]
    [SerializeField] private string _saveFileName   = "exploration_save.json";
    [SerializeField] private string _saveFolderName = "Saves";

    [Header("Reset")]
    [Tooltip("Spawn do Wulfric ao fazer reset. Se vazio, procura 'ResetWulfricPosition' na cena.")]
    [SerializeField] private Transform _wulfricResetSpawn;

    // ── Singleton ─────────────────────────────────────────────────────────

    public static ExplorationLoadContext Instance { get; private set; }

    /// <summary>Disparado após snapshots ou defaults serem aplicados aos personagens da cena.</summary>
    public static event Action OnExplorationStateApplied;

    // ── Serviço de IO ─────────────────────────────────────────────────────

    private readonly IFileService _fileService = new FileService();

    // ── Estado interno ────────────────────────────────────────────────────

    private List<PlayableCharacterSnapshot> _snapshots = new();
    private bool   _hasSave;
    private bool   _preferInMemorySnapshotsOnNextRestore;
    private string _saveDirectory;
    private float  _savedCorruptionValue;

    private PlayableCharactersManager _manager;

    private readonly Dictionary<string, VillageSpawnSnapshot> _villageSpawnByCharacterName =
        new(StringComparer.OrdinalIgnoreCase);

    [Serializable]
    private struct VillageSpawnSnapshot
    {
        public Vector3    Position;
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

        // ── Corrupção: lê o arquivo primeiro (awaited) ───────────────────
        if (_corruptionSystem != null)
            await _corruptionSystem.LoadAsync();

        // ── Personagens ──────────────────────────────────────────────────
        if (_preferInMemorySnapshotsOnNextRestore)
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
            ApplySnapshots();
        else
            ApplyDefaultSetups();

        // ── Corrupção: aplica o valor já carregado ───────────────────────
        if (_corruptionSystem != null)
        {
            _corruptionSystem.RestoreState();

            if (shouldApplySavedSnapshots)
            {
                _corruptionSystem.Corruption = Mathf.Clamp(_savedCorruptionValue, 0f, 100f);
            }
        }
        else if (_savedCorruptionValue > 0f)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[LOAD] Corrupção em memória ({_savedCorruptionValue:F1}) sem ExplorationCorruptionSystem na cena.",
                LogCategory.Player);
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

    public IReadOnlyDictionary<string, ExplorationHealthSnapshot> GetSavedHealthByCharacterName()
    {
        var healthByCharacterName = new Dictionary<string, ExplorationHealthSnapshot>(StringComparer.OrdinalIgnoreCase);
        foreach (var snapshot in _snapshots)
        {
            if (string.IsNullOrWhiteSpace(snapshot.CharacterName))
            {
                continue;
            }

            healthByCharacterName[snapshot.CharacterName] =
                new ExplorationHealthSnapshot(snapshot.CurrentHealth, snapshot.MaxHealth);
        }

        return healthByCharacterName;
    }

    public double GetSavedCorruptionValue()
    {
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
    /// Atualiza snapshots em memória com HP/corrupção pós-combate e,
    /// em derrota, reposiciona o grupo na aldeia.
    /// </summary>
    public void ApplyCombatOutcomeToSnapshots(
        Game.Core.Models.BattleState battleState,
        IReadOnlyList<string> combatAllyCharacterNames,
        bool returnToVillage,
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
            snapshot.CurrentHealth = ally.Health.CurrentHp;
            snapshot.MaxHealth     = ally.Health.MaxHp;
        }

        _savedCorruptionValue = (float)Math.Max(0, battleState.CorruptionValue);
        if (_corruptionSystem != null)
        {
            _corruptionSystem.Corruption = Mathf.Clamp(_savedCorruptionValue, 0f, 100f);
        }

        if (returnToVillage)
        {
            OverwriteSnapshotsForVillageReturn();
        }

        _hasSave = _snapshots.Count > 0;
        if (_hasSave && persistToDisk)
        {
            _ = SaveToFileAsync();
        }

        if (_corruptionSystem != null)
        {
            _corruptionSystem.SaveState();
        }
    }

    /// <summary>
    /// Grava snapshots em memória e carrega a cena de exploração após retorno de combate.
    /// Evita race: nudge pós-vitória deve ser persistido antes do reload.
    /// </summary>
    public void FinishCombatReturnAndLoadExploration(string sceneName)
    {
        StartCoroutine(FinishCombatReturnAndLoadExplorationRoutine(sceneName));
    }

    // ── IO (personagens) ──────────────────────────────────────────────────

    private async System.Threading.Tasks.Task SaveToFileAsync()
    {
        try
        {
            var saveData = new SnapshotSaveData
            {
                Snapshots       = _snapshots,
                CorruptionValue = _savedCorruptionValue,
            };
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
                _snapshots.Clear();
                _hasSave              = false;
                _savedCorruptionValue = 0f;
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[LOAD] Nenhum arquivo de save encontrado.", LogCategory.Player);
                return;
            }

            FileData fileData = await _fileService.ReadAsync(_saveFileName, _saveDirectory);
            var saveData = JsonUtility.FromJson<SnapshotSaveData>(fileData._fileContent);

            if (saveData?.Snapshots != null && saveData.Snapshots.Count > 0)
            {
                _snapshots            = saveData.Snapshots;
                _savedCorruptionValue = saveData.CorruptionValue;
                _hasSave              = true;

                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[LOAD] {_snapshots.Count} snapshots carregados de '{fileData.FullPath}'.",
                    LogCategory.Player);
            }
            else
            {
                _snapshots.Clear();
                _hasSave              = false;
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

    private IEnumerator FinishCombatReturnAndLoadExplorationRoutine(string sceneName)
    {
        _preferInMemorySnapshotsOnNextRestore = true;
        _hasSave = _snapshots.Count > 0;

        var saveTask = SaveToFileAsync();
        while (!saveTask.IsCompleted)
        {
            yield return null;
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

            if (character.HealthBar != null && snap.MaxHealth > 0f)
            {
                character.HealthBar.SetMaxHealth(snap.MaxHealth, keepRatio: false);
                character.HealthBar.Kill();
                character.HealthBar.Heal(snap.CurrentHealth);

                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[LOAD] '{character.CharacterName}' HP → {snap.CurrentHealth}/{snap.MaxHealth}",
                    LogCategory.Player);
            }

            character.Transform.SetPositionAndRotation(snap.Position, snap.Rotation);
            _manager.SetState(snap.State, character);

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[LOAD] '{character.CharacterName}' → {snap.State} @ {snap.Position}",
                LogCategory.Player);
        }

        if (_corruptionSystem != null)
        {
            _corruptionSystem.Corruption = Mathf.Clamp(_savedCorruptionValue, 0f, 100f);
        }

        _manager.NotifyCurrentMainIfAny();
        NotifyExplorationStateApplied();
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

                float startingHealth = setup.StartingHealth > 0f
                    ? Mathf.Clamp(setup.StartingHealth, 0f, setup.MaxHealth)
                    : setup.MaxHealth;

                healthBar.Kill();
                healthBar.Heal(startingHealth);

                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[LOAD] '{setup.Character.CharacterName}' HP padrão → {startingHealth}/{setup.MaxHealth}",
                    LogCategory.Player);
            }

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[LOAD] '{setup.Character.CharacterName}' (padrão) → {setup.InitialState}",
                LogCategory.Player);
        }

        _manager.NotifyCurrentMainIfAny();
        NotifyExplorationStateApplied();
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
            PlayableCharacterState.Main      => 0,
            PlayableCharacterState.Companion => 1,
            PlayableCharacterState.Resting   => 2,
            _                                => 3,
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
                MaxHealth = ResolveExplorationMaxHealth(playableCharacter.CharacterName),
                StartingHealth = ResolveDefaultStartingHealth(playableCharacter.CharacterName),
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

    private static float ResolveExplorationMaxHealth(string characterName)
    {
        return characterName switch
        {
            "Wulfric" => 100f,
            "Buck" => 200f,
            "Matsuda" => 100f,
            _ => 100f,
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
}