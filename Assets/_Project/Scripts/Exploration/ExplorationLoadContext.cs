using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core.Models;
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
    public float MaxHealth;
    public Vector3 RestingPoint;

    public PlayableCharacterSnapshot(
        string name, Vector3 pos, Quaternion rot,
        PlayableCharacterState state,
        float currentHealth, float maxHealth, Vector3 restingPoint)
    {
        CharacterName = name;
        Position = pos;
        Rotation = rot;
        State = state;
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
        RestingPoint = restingPoint;
    }
}

public readonly struct ExplorationHealthSnapshot
{
    public float CurrentHealth { get; }
    public float MaxHealth { get; }

    public ExplorationHealthSnapshot(float currentHealth, float maxHealth)
    {
        CurrentHealth = currentHealth;
        MaxHealth = maxHealth;
    }
}

// ── Wrapper para serialização JSON ───────────────────────────────────────────

[Serializable]
internal sealed class SnapshotSaveData
{
    public List<PlayableCharacterSnapshot> Snapshots = new();
    public double CorruptionValue;
}

// ── Configuração de estado padrão ────────────────────────────────────────────

[Serializable]
public struct DefaultCharacterSetup
{
    public PlayableCharacter Character;
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
    [SerializeField] private string _saveFileName = "exploration_save.json";
    [SerializeField] private string _saveFolderName = "Saves";

    // ── Singleton ─────────────────────────────────────────────────────────

    public static ExplorationLoadContext Instance { get; private set; }

    // ── Serviço de IO ─────────────────────────────────────────────────────

    private readonly IFileService _fileService = new FileService();

    // ── Estado interno ────────────────────────────────────────────────────

    private List<PlayableCharacterSnapshot> _snapshots = new();
    private bool _hasSave;
    private string _saveDirectory;
    private double _savedCorruptionValue;

    private PlayableCharactersManager _manager;

    private readonly Dictionary<string, VillageSpawnSnapshot> _villageSpawnByCharacterName =
        new(StringComparer.OrdinalIgnoreCase);

    [Serializable]
    private struct VillageSpawnSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
    }

    public List<PlayableCharacterSnapshot> Snapshots => _snapshots;

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

        if (GetComponent<CombatExplorationBridge>() == null)
            gameObject.AddComponent<CombatExplorationBridge>();

        _saveDirectory = System.IO.Path.Combine(Application.persistentDataPath, _saveFolderName);
    }

    private void AdoptSceneConfigurationFrom(ExplorationLoadContext sceneInstance)
    {
        if (sceneInstance == null)
        {
            return;
        }

        _explorationSceneName = sceneInstance._explorationSceneName;
        _defaultSetups = sceneInstance._defaultSetups;
        if (sceneInstance._corruptionSystem != null)
        {
            _corruptionSystem = sceneInstance._corruptionSystem;
        }
    }

    private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    private void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void Start()
    {
        if (IsConfiguredExplorationScene(SceneManager.GetActiveScene().name))
        {
            TryRestoreOnSceneReady();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsConfiguredExplorationScene(scene.name))
        {
            StartCoroutine(RestoreNextFrame());
        }
    }

    private bool IsConfiguredExplorationScene(string sceneName)
    {
        if (string.Equals(sceneName, _explorationSceneName, StringComparison.Ordinal))
        {
            return true;
        }

        return string.Equals(sceneName, "Overworld", StringComparison.Ordinal)
               || string.Equals(sceneName, "Exploration", StringComparison.Ordinal);
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
                hp?.MaxHealth ?? 0f,
                character.RestingPoint != null ? character.RestingPoint.position : character.Transform.position));
        }

        if (_corruptionSystem != null)
        {
            _savedCorruptionValue = Math.Max(0, _corruptionSystem.Corruption);
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
        {
            await _corruptionSystem.LoadAsync();
            _corruptionSystem.RestoreState();

            if (_hasSave)
            {
                _corruptionSystem.Corruption = Mathf.Clamp((float)_savedCorruptionValue, 0f, 100f);
            }
        }

        // ── Personagens ──────────────────────────────────────────────────
        if (!_hasSave || _snapshots.Count == 0)
            await LoadFromFileAsync();

        if (_hasSave && _snapshots.Count > 0)
            ApplySnapshotsAndSave();
        else
            ApplyDefaultSetups();

        // Corrupção já aplicada acima quando _corruptionSystem != null.
    }

    /// <summary>
    /// Limpa o save em memória, remove os arquivos em disco e zera a corrupção (novo jogo).
    /// </summary>
    public async void ClearSave()
    {
        // ── Personagens ──────────────────────────────────────────────────
        _snapshots.Clear();
        _hasSave = false;
        _savedCorruptionValue = 0;

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

    public IReadOnlyDictionary<string, ExplorationHealthSnapshot> GetSavedHealthByCharacterName()
    {
        var healthByCharacterName = new Dictionary<string, ExplorationHealthSnapshot>(StringComparer.OrdinalIgnoreCase);

        foreach (var snapshot in _snapshots)
        {
            if (snapshot.MaxHealth <= 0f)
            {
                continue;
            }

            healthByCharacterName[snapshot.CharacterName] = new ExplorationHealthSnapshot(
                snapshot.CurrentHealth,
                snapshot.MaxHealth);
        }

        return healthByCharacterName;
    }

    public double GetSavedCorruptionValue()
    {
        if (_hasSave || _savedCorruptionValue > 0)
        {
            return Math.Max(0, _savedCorruptionValue);
        }

        if (_corruptionSystem != null)
        {
            return Math.Max(0, _corruptionSystem.Corruption);
        }

        return 0;
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
    /// Atualiza snapshots em memória com o resultado do combate e persiste para o retorno à exploração.
    /// </summary>
    public void ApplyCombatOutcomeToSnapshots(
        BattleState battleState,
        IReadOnlyList<string> allyCharacterNames,
        bool returnToVillage)
    {
        if (battleState == null)
        {
            return;
        }

        var allies = battleState.Allies;
        for (int allyIndex = 0; allyIndex < allies.Count && allyIndex < allyCharacterNames.Count; allyIndex++)
        {
            var characterName = allyCharacterNames[allyIndex];
            var ally = allies[allyIndex];
            var snapshot = _snapshots.Find(savedSnapshot =>
                string.Equals(savedSnapshot.CharacterName, characterName, StringComparison.Ordinal));

            if (snapshot == null)
            {
                continue;
            }

            snapshot.CurrentHealth = ally.Health.CurrentHp;
            snapshot.MaxHealth = ally.Health.MaxHp;
        }

        _savedCorruptionValue = Math.Max(0, battleState.CorruptionValue);

        if (returnToVillage)
        {
            OverwriteSnapshotsForVillageReturn();
        }

        _hasSave = _snapshots.Count > 0;
        _ = SaveToFileAsync();
        _ = PersistCorruptionValueAsync((float)_savedCorruptionValue);

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[COMBAT-RETURN] Snapshots atualizados — corrupção {_savedCorruptionValue:F1}, vila={returnToVillage}.",
            LogCategory.Player);
    }

    public void NudgeSnapshotsAwayFromWorldPoint(Vector3 worldPoint, float separationDistance)
    {
        if (_snapshots == null || _snapshots.Count == 0 || separationDistance <= 0f)
        {
            return;
        }

        foreach (var snapshot in _snapshots)
        {
            Vector3 toCharacter = snapshot.Position - worldPoint;
            float currentDistance = toCharacter.magnitude;

            if (currentDistance >= separationDistance)
            {
                continue;
            }

            Vector3 direction = currentDistance > 0.01f
                ? toCharacter / currentDistance
                : Vector3.forward;

            snapshot.Position = worldPoint + direction * separationDistance;
        }
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
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[LOAD] Nenhum arquivo de save encontrado.", LogCategory.Player);
                return;
            }

            FileData fileData = await _fileService.ReadAsync(_saveFileName, _saveDirectory);
            var saveData = JsonUtility.FromJson<SnapshotSaveData>(fileData._fileContent);

            if (saveData?.Snapshots != null && saveData.Snapshots.Count > 0)
            {
                _snapshots = saveData.Snapshots;
                _hasSave = true;
                _savedCorruptionValue = Math.Max(0, saveData.CorruptionValue);

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
    public void ApplySnapshotsAndSave()
    {
        if (!TryGetManager()) return;

        if (_snapshots == null || _snapshots.Count == 0)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[LOAD] ApplySnapshotsAndSave chamado sem snapshots em memória.",
                LogCategory.Player);
            return;
        }

        // Marca como tendo save para que ApplySnapshots entre no caminho correto
        _hasSave = true;

        ApplySnapshots(); // move os personagens na cena

        SaveState();      // relê a cena (já com posições corretas) e grava no disco
    }
    private void ApplyDefaultSetups()
    {
        RemoveDestroyedDefaultSetups();

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

    // ── Helpers ───────────────────────────────────────────────────────────

    private void CacheVillageSpawnPointsFromActiveScene()
    {
        if (!IsConfiguredExplorationScene(SceneManager.GetActiveScene().name))
        {
            return;
        }

        var restingPointsRoot = GameObject.Find("Resting Points");
        if (restingPointsRoot == null)
        {
            return;
        }

        _villageSpawnByCharacterName.Clear();
        foreach (Transform childTransform in restingPointsRoot.transform)
        {
            _villageSpawnByCharacterName[childTransform.name] = new VillageSpawnSnapshot
            {
                Position = childTransform.position,
                Rotation = childTransform.rotation,
            };
        }
    }

    private void OverwriteSnapshotsForVillageReturn()
    {
        if (_villageSpawnByCharacterName.Count == 0)
        {
            foreach (var snapshot in _snapshots)
            {
                if (snapshot.RestingPoint != default)
                {
                    snapshot.Position = snapshot.RestingPoint;
                }
            }

            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[COMBAT-RETURN] Cache da vila vazio — usado RestingPoint dos snapshots.",
                LogCategory.Player);
            return;
        }

        foreach (var snapshot in _snapshots)
        {
            if (_villageSpawnByCharacterName.TryGetValue(snapshot.CharacterName, out var villageSpawn))
            {
                snapshot.Position = villageSpawn.Position;
                snapshot.Rotation = villageSpawn.Rotation;
                continue;
            }

            if (snapshot.RestingPoint != default)
            {
                snapshot.Position = snapshot.RestingPoint;
            }
        }
    }

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

    private void TryResolveCorruptionSystemFromScene()
    {
        if (_corruptionSystem != null)
        {
            return;
        }

        _corruptionSystem = FindFirstObjectByType<ExplorationCorruptionSystem>();
    }

    private async System.Threading.Tasks.Task PersistCorruptionValueAsync(float corruptionValue)
    {
        if (_corruptionSystem != null)
        {
            _corruptionSystem.Corruption = Mathf.Clamp(corruptionValue, 0f, 100f);
            _corruptionSystem.SaveState();
            return;
        }

        try
        {
            var corruptionSaveData = new CorruptionSaveData { Corruption = Mathf.Clamp(corruptionValue, 0f, 100f) };
            string json = JsonUtility.ToJson(corruptionSaveData, prettyPrint: true);
            var fileData = new FileData(json, "corruption_save.json", _saveDirectory);
            await _fileService.WriteAsync(fileData);
        }
        catch (Exception ex)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                $"[COMBAT-RETURN] Falha ao persistir corrupção: {ex.Message}",
                LogCategory.Player);
        }
    }

    private bool TryGetManager()
    {
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