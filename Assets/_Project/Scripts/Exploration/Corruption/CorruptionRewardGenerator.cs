using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core.Config;
using Services.DebugUtilities;
using Services.IO;
using Services.Loot;
using UnityEngine;
using Core.Exploration.Interactables.Chest;
/// <summary>
/// Gera recompensas baseadas no tier de corrupção lido em Awake.
///
/// Fluxo:
///   Awake         → lê o arquivo de corrupção e determina o tier (0-4) via
///                   <see cref="CorruptionTierCalculator"/>.
///   GenerateRewards() → gera loot da <see cref="LootTable"/> correspondente
///                   ao tier, transfere ao <see cref="PlayerInventorySystem"/>
///                   e persiste via <see cref="PlayerInventorySaveSystem"/>.
/// </summary>
public sealed class CorruptionRewardGenerator : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Referências")]
    [SerializeField] private PlayerInventorySystem     _inventory;
    [SerializeField] private PlayerInventorySaveSystem _inventorySave;

    [Header("LootTables por Tier (índice = tier 0-4)")]
    [Tooltip("Deve ter exatamente 5 entradas: tiers 0, 1, 2, 3 e 4.")]
    [SerializeField] private LootTable[] _lootTablesByTier = new LootTable[5];

    [Header("IO — deve coincidir com ExplorationCorruptionSystem")]
    [SerializeField] private string _saveFolderName  = "Saves";
    [SerializeField] private string _saveFileName    = "corruption_save.json";

    // ── Estado ────────────────────────────────────────────────────────────

    /// <summary>Tier resolvido no Awake (0-4). -1 = não inicializado.</summary>
    public int ResolvedTier { get; private set; } = -1;

    /// <summary>Último loot gerado.</summary>
    public IReadOnlyDictionary<IStorageable, int> LastReward => _lastReward;

    private IReadOnlyDictionary<IStorageable, int> _lastReward =
        new Dictionary<IStorageable, int>();

    private ILootService _lootService = new LootService();

    private readonly IFileService _fileService = new FileService();
    private string _saveDirectory;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        _saveDirectory = System.IO.Path.Combine(
            Application.persistentDataPath, _saveFolderName);

        _ = InitializeTierAsync();
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Gera recompensas com base no tier resolvido, adiciona ao inventário
    /// e salva em disco. Retorna false se o tier ainda não foi resolvido ou
    /// não há LootTable configurada para o tier atual.
    /// </summary>
    public void GenerateRewards()
    {
        if (ResolvedTier < 0)
        {
            Log(LogLevel.Warning, "Tier ainda não resolvido — aguarde Awake concluir.");
        }

        LootTable table = GetTableForTier(ResolvedTier);
        if (table == null)
        {
            Log(LogLevel.Warning,
                $"Nenhuma LootTable configurada para tier {ResolvedTier}. Recompensa não gerada.");
        }

        var context = new LootRequestContext(gameObject.name, transform.position);
        _lastReward = _lootService.GenerateLoot(table, context);

        Log(LogLevel.Debug,
            $"Recompensa gerada — Tier {ResolvedTier} | {_lastReward.Count} tipo(s) de item.");

        TransferToInventory();
        SaveInventory();
    }

    /// <summary>Injeta um <see cref="ILootService"/> alternativo (testes / dificuldade).</summary>
    public void InjectLootService(ILootService service) =>
        _lootService = service ?? throw new ArgumentNullException(nameof(service));

    // ── Inicialização assíncrona do tier ──────────────────────────────────

    private async Task InitializeTierAsync()
    {
        double corruption = await ReadCorruptionFromFileAsync();
        ResolvedTier = CorruptionTierCalculator.GetTier(corruption);

        Log(LogLevel.Debug,
            $"Corrupção lida: {corruption:F1}% → Tier resolvido: {ResolvedTier}");
    }

    private async Task<double> ReadCorruptionFromFileAsync()
    {
        try
        {
            bool exists = await _fileService.ExistsAsync(_saveFileName, _saveDirectory);
            if (!exists)
            {
                Log(LogLevel.Debug, "Sem arquivo de save de corrupção — usando 0 (Tier 0).");
                return 0.0;
            }

            FileData fileData = await _fileService.ReadAsync(_saveFileName, _saveDirectory);
            var data = JsonUtility.FromJson<CorruptionSaveData>(fileData._fileContent);

            if (data == null)
            {
                Log(LogLevel.Warning, "Falha ao desserializar save de corrupção — usando 0.");
                return 0.0;
            }

            Log(LogLevel.Debug, $"Corrupção lida do disco: {data.Corruption:F1}%");
            return data.Corruption;
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Erro ao ler corrupção: {ex.Message} — usando 0.");
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

    // ── Gizmos ────────────────────────────────────────────────────────────

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
