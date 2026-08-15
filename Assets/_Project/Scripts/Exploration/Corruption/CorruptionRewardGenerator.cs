using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Config;
using Services.DebugUtilities;
using Services.IO;
using Services.Loot;
using UnityEngine;
using Core.Exploration.Interactables.Chest;
using Unity.VisualScripting;

/// <summary>
/// Gera recompensas baseadas no tier de corrupção.
/// A corrupção é lida do disco diretamente em <see cref="GenerateRewards"/>,
/// eliminando qualquer dependência de ordem de inicialização.
/// </summary>
public sealed class CorruptionRewardGenerator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Referências")]
    [SerializeField] private PlayerInventorySystem _inventory;
    [SerializeField] private PlayerInventorySaveSystem _inventorySave;

    [Header("LootTables por Tier (índice = tier 0-4)")]
    [Tooltip("Deve ter exatamente 5 entradas: tiers 0, 1, 2, 3 e 4.")]
    [SerializeField] private LootTable[] _lootTablesByTier = new LootTable[5];

    [Header("IO — deve coincidir com ExplorationCorruptionSystem")]
    [SerializeField] private string _saveFolderName = "Saves";
    [SerializeField] private string _saveFileName = "corruption_save.json";
    [SerializeField] private GameObject rewardViewParent;
    [SerializeField] private GameObject rewardPrefab;

    // ── Estado ────────────────────────────────────────────────────────────

    /// <summary>Tier resolvido na última chamada a GenerateRewards. -1 = nunca gerou.</summary>
    public int ResolvedTier { get; private set; } = -1;

    /// <summary>Último loot gerado.</summary>
    public IReadOnlyDictionary<IStorageable, int> LastReward => _lastReward;

    private IReadOnlyDictionary<IStorageable, int> _lastReward =
        new Dictionary<IStorageable, int>();

    private ILootService _lootService = new LootService();
    private readonly IFileService _fileService = new FileService();

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Lê a corrupção do disco, resolve o tier, gera recompensas,
    /// transfere ao inventário e salva. Tudo numa única chamada async.
    /// </summary>
    public async void GenerateRewards()
    {
        var saveDirectory = System.IO.Path.Combine(
            Application.persistentDataPath, _saveFolderName);

        Log(LogLevel.Debug, $"[GenerateRewards] Iniciando leitura de corrupção. Diretório: {saveDirectory}");

        double corruption = await ReadCorruptionFromFileAsync(saveDirectory);

        Log(LogLevel.Debug, $"[GenerateRewards] Valor de corrupção lido: {corruption:F2}%");

        ResolvedTier = CorruptionTierCalculator.GetTier(corruption);

        Log(LogLevel.Debug,
            $"[GenerateRewards] Tier resolvido: {ResolvedTier} " +
            $"(corrupção {corruption:F2} | faixas: 0=≤32 / 1=≤65 / 2=≤98 / 3=≤198 / 4=>198)");

        LootTable table = GetTableForTier(ResolvedTier);

        Log(LogLevel.Debug,
            $"[GenerateRewards] LootTable para tier {ResolvedTier}: " +
            $"{(table != null ? table.name : "NULL — nenhuma tabela configurada")}");

        if (table == null)
        {
            Log(LogLevel.Warning,
                $"[GenerateRewards] Nenhuma LootTable configurada para tier {ResolvedTier}. " +
                $"Verifique o array _lootTablesByTier no Inspector (tamanho atual: {_lootTablesByTier?.Length ?? 0}). " +
                $"Recompensa não gerada.");
            return;
        }

        var context = new LootRequestContext(gameObject.name, transform.position);
        _lastReward = _lootService.GenerateLoot(table, context);

        Log(LogLevel.Debug,
            $"[GenerateRewards] Recompensa gerada — Tier {ResolvedTier} | " +
            $"LootTable '{table.name}' | {_lastReward.Count} tipo(s) de item.");

        foreach (var (storageable, amount) in _lastReward)
        {
            Log(LogLevel.Debug,
                $"[GenerateRewards]   → {storageable?.GetType().Name ?? "null"} x{amount}");
        }

        TransferToInventory();
        SaveInventory();
    }

    /// <summary>Injeta um <see cref="ILootService"/> alternativo (testes / dificuldade).</summary>
    public void InjectLootService(ILootService service) =>
        _lootService = service ?? throw new ArgumentNullException(nameof(service));

    // ── IO ────────────────────────────────────────────────────────────────

    private async Task<double> ReadCorruptionFromFileAsync(string saveDirectory)
    {
        var fullPath = System.IO.Path.Combine(saveDirectory, _saveFileName);
        Log(LogLevel.Debug, $"[ReadCorruption] Caminho completo do arquivo: {fullPath}");

        try
        {
            bool exists = await _fileService.ExistsAsync(_saveFileName, saveDirectory);
            Log(LogLevel.Debug, $"[ReadCorruption] Arquivo existe: {exists}");

            if (!exists)
            {
                Log(LogLevel.Debug, "[ReadCorruption] Sem arquivo de save de corrupção — usando 0 (Tier 0).");
                return 0.0;
            }

            FileData fileData = await _fileService.ReadAsync(_saveFileName, saveDirectory);
            Log(LogLevel.Debug, $"[ReadCorruption] Conteúdo bruto lido: {fileData._fileContent}");

            var data = JsonUtility.FromJson<CorruptionSaveData>(fileData._fileContent);

            if (data == null)
            {
                Log(LogLevel.Warning, "[ReadCorruption] Falha ao desserializar CorruptionSaveData — usando 0.");
                return 0.0;
            }

            Log(LogLevel.Debug, $"[ReadCorruption] Corrupção desserializada: {data.Corruption:F2}%");
            return data.Corruption;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"[ReadCorruption] Exceção ao ler arquivo '{fullPath}': {ex.Message} — usando 0.");
            return 0.0;
        }
    }

    // ── Transferência e save ──────────────────────────────────────────────

    private void TransferToInventory()
    {
        if (_lastReward.Count == 0)
        {
            Log(LogLevel.Debug, "Loot vazio — nada a transferir.");
            return;
        }

        if (_inventory == null)
        {
            Log(LogLevel.Error,
                "PlayerInventorySystem não atribuído — itens não transferidos.");
            return;
        }

        _inventory.AddItems(new Dictionary<IStorageable, int>(_lastReward));

        Log(LogLevel.Debug,
            $"{_lastReward.Count} tipo(s) de item transferido(s) ao inventário.");

        SpawnRewardView();
    }

    private void SpawnRewardView()
    {
        if (rewardPrefab == null || rewardViewParent == null)
        {
            Log(LogLevel.Warning, "rewardPrefab ou rewardViewParent não atribuído — visualização ignorada.");
            return;
        }

        foreach (var (storageable, amount) in _lastReward)
        {
            var instance = Instantiate(rewardPrefab, rewardViewParent.transform);
            ApplyRewardVisual(instance, storageable, amount);
        }
    }

    private void ApplyRewardVisual(GameObject rewardInstance, IStorageable storageable, int amount)
    {
        var view = rewardInstance.GetComponent<RewardView>();
        if (view == null)
        {
            Log(LogLevel.Warning, "RewardView não encontrado no rewardPrefab.");
            return;
        }

        view.UpdateView(storageable, amount);
    }

    private void SaveInventory()
    {
        if (_inventorySave == null)
        {
            Log(LogLevel.Warning,
                "PlayerInventorySaveSystem não atribuído — inventário não salvo.");
            return;
        }

        _inventorySave.SaveAsync();
        Log(LogLevel.Debug, "Inventário persistido após recompensa.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private LootTable GetTableForTier(int tier)
    {
        if (_lootTablesByTier == null || tier < 0 || tier >= _lootTablesByTier.Length)
            return null;

        return _lootTablesByTier[tier];
    }

    private static void Log(LogLevel level, string msg) =>
        LoggerService.PrintLogMessage(level,
            $"[CorruptionRewardGenerator] {msg}", LogCategory.Player);

    // ── Editor ────────────────────────────────────────────────────────────

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_lootTablesByTier != null && _lootTablesByTier.Length != 5)
        {
            Array.Resize(ref _lootTablesByTier, 5);
            Debug.LogWarning(
                "[CorruptionRewardGenerator] _lootTablesByTier redimensionado para 5 entradas (tiers 0-4).");
        }
    }
#endif
}