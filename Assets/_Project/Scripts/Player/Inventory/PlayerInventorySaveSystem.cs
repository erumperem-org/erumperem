using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using Services.IO;
using UnityEngine;

// ── DTO de serialização ───────────────────────────────────────────────────────

[Serializable]
internal sealed class InventoryEntry
{
    public string ItemId;
    public int    Amount;

    public InventoryEntry(string itemId, int amount)
    {
        ItemId = itemId;
        Amount = amount;
    }
}

[Serializable]
internal sealed class InventorySaveData
{
    public List<InventoryEntry> Entries = new();
}

// ── PlayerInventorySaveSystem ─────────────────────────────────────────────────

/// <summary>
/// Persiste e restaura o <see cref="PlayerInventorySystem"/> em disco (JSON).
///
/// • <c>Awake</c>  → tenta carregar; se o arquivo não existir, mantém inventário vazio.
/// • <c>SaveAsync</c>  → serializa o inventário atual e grava em disco.
/// • <c>LoadAsync</c>  → lê o arquivo e reaplica os itens no inventário.
/// • <c>ClearSave</c>  → apaga o arquivo e limpa o inventário em memória.
///
/// Requer que cada <see cref="IStorageable"/> implemente <c>ItemId</c> (string única)
/// e que um <see cref="ItemRegistry"/> possa resolver o id de volta ao objeto.
/// </summary>
public sealed class PlayerInventorySaveSystem : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Referências")]
    [SerializeField] private PlayerInventorySystem _inventory;

    [Tooltip("Registro de todos os IStorageable do projeto, usado para resolver ItemId → objeto.")]
    [SerializeField] private ItemRegistry _registry;

    [Header("IO")]
    [SerializeField] private string _saveFileName   = "inventory_save.json";
    [SerializeField] private string _saveFolderName = "Saves";

    // ── Serviço de IO ─────────────────────────────────────────────────────

    private readonly IFileService _fileService = new FileService();
    private string _saveDirectory;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        _saveDirectory = System.IO.Path.Combine(Application.persistentDataPath, _saveFolderName);
        _ = LoadAsync(); // fire-and-forget; inventário começa vazio se arquivo não existir
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>Serializa o inventário atual e grava em disco.</summary>
    public async void SaveAsync()
    {
        var saveData = new InventorySaveData();

        foreach (var kvp in _inventory.GetAll())
        {
            if (kvp.Key == null) continue;
            saveData.Entries.Add(new InventoryEntry(kvp.Key.ItemId, kvp.Value));
        }

        try
        {
            string json     = JsonUtility.ToJson(saveData, prettyPrint: true);
            var    fileData = new FileData(json, _saveFileName, _saveDirectory);
            await _fileService.WriteAsync(fileData);

            Log(LogLevel.Debug, $"Save gravado em: {fileData.FullPath} ({saveData.Entries.Count} entradas)");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Falha ao gravar save: {ex.Message}");
        }
    }

    /// <summary>
    /// Lê o arquivo de save e reaplica os itens no inventário.
    /// Se o arquivo não existir, mantém o inventário vazio sem erro.
    /// </summary>
    public async System.Threading.Tasks.Task LoadAsync()
    {
        try
        {
            bool exists = await _fileService.ExistsAsync(_saveFileName, _saveDirectory);
            if (!exists)
            {
                Log(LogLevel.Debug, "Nenhum arquivo de save encontrado — inventário iniciado vazio.");
                return;
            }

            FileData fileData = await _fileService.ReadAsync(_saveFileName, _saveDirectory);
            var saveData = JsonUtility.FromJson<InventorySaveData>(fileData._fileContent);

            if (saveData?.Entries == null || saveData.Entries.Count == 0)
            {
                Log(LogLevel.Debug, "Arquivo de save vazio — inventário iniciado vazio.");
                return;
            }

            // Reconstrói o dicionário a partir dos ids
            var toAdd = new Dictionary<IStorageable, int>();
            foreach (var entry in saveData.Entries)
            {
                IStorageable item = _registry.Resolve(entry.ItemId);
                if (item == null)
                {
                    Log(LogLevel.Warning, $"ItemId '{entry.ItemId}' não encontrado no registry — ignorado.");
                    continue;
                }
                toAdd[item] = entry.Amount;
            }

            if (toAdd.Count > 0)
                _inventory.AddItems(toAdd);

            Log(LogLevel.Debug, $"{toAdd.Count} item(s) restaurados do save.");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Falha ao ler save: {ex.Message}");
        }
    }

    /// <summary>Apaga o arquivo de save e limpa o inventário em memória.</summary>
    public async void ClearSave()
    {
        try
        {
            await _fileService.DeleteAsync(_saveFileName, _saveDirectory);
            Log(LogLevel.Debug, "Arquivo de save deletado.");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, $"Falha ao deletar save: {ex.Message}");
        }
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private static void Log(LogLevel level, string msg) =>
        LoggerService.PrintLogMessage(level, $"[InventorySave] {msg}", LogCategory.Inventory);
}
