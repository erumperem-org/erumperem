using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using Services.IO;
using UnityEngine;
using System.Threading.Tasks;

// ── DTO de serialização ───────────────────────────────────────────────────────

[Serializable]
internal sealed class InventoryEntry
{
    public string ItemId;
    public int Amount;

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
/// • Singleton: acesse via <see cref="Instance"/>.
/// • <c>Awake</c>      → tenta carregar; se o arquivo não existir, mantém inventário vazio.
/// • <c>SaveAsync</c>  → serializa o inventário atual e grava em disco.
/// • <c>LoadAsync</c>  → lê o arquivo e reaplica os itens no inventário.
/// • <c>ClearSave</c>  → apaga o arquivo e limpa o inventário em memória.
///
/// Requer que cada <see cref="IStorageable"/> implemente <c>ItemId</c> (string única)
/// e que um <see cref="ItemRegistry"/> possa resolver o id de volta ao objeto.
/// </summary>
public sealed class PlayerInventorySaveSystem : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────

    public static PlayerInventorySaveSystem Instance { get; private set; }

    // ── Inspector ─────────────────────────────────────────────────────────

    [Header("Referências")]
    [SerializeField] private PlayerInventorySystem _inventory;

    [Tooltip("Registro de todos os IStorageable do projeto, usado para resolver ItemId → objeto.")]
    [SerializeField] private ItemRegistry _registry;

    [Header("IO")]
    [SerializeField] private string _saveFileName = "inventory_save.json";
    [SerializeField] private string _saveFolderName = "Saves";

    // ── Serviço de IO ─────────────────────────────────────────────────────

    private readonly IFileService _fileService = new FileService();
    private string _saveDirectory;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Log(LogLevel.Warning, "Instância duplicada detectada — destruindo.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _saveDirectory = System.IO.Path.Combine(Application.persistentDataPath, _saveFolderName);
        _ = LoadAsync();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
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
            string json = JsonUtility.ToJson(saveData, prettyPrint: true);
            var fileData = new FileData(json, _saveFileName, _saveDirectory);
            await _fileService.WriteAsync(fileData);

            Log(LogLevel.Debug, $"Save gravado em: {fileData.FullPath} ({saveData.Entries.Count} entradas)");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Error, $"Falha ao gravar save: {ex.Message}");
        }
    }

    /// <summary>Apaga o arquivo de save.</summary>
    public async System.Threading.Tasks.Task ClearSave()
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
    public async void DeletesSave()
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

    /// <summary>Apaga o arquivo de save E limpa o inventário em memória.</summary>
    public async Task DeletesSaveAsync()
    {
        try
        {
            _inventory.Clear(); // limpa memória imediatamente
            await _fileService.DeleteAsync(_saveFileName, _saveDirectory);
            Log(LogLevel.Debug, "Save de inventário deletado e memória limpa.");
        }
        catch (Exception ex)
        {
            Log(LogLevel.Warning, $"Falha ao deletar save: {ex.Message}");
        }
    }

    public async Task LoadAsync()
    {
        try
        {
            bool exists = await _fileService.ExistsAsync(_saveFileName, _saveDirectory);
            if (!exists)
            {
                Log(LogLevel.Debug, "Nenhum arquivo de save encontrado — inventário vazio.");
                return;
            }

            FileData fileData = await _fileService.ReadAsync(_saveFileName, _saveDirectory);
            var saveData = JsonUtility.FromJson<InventorySaveData>(fileData._fileContent);

            if (saveData?.Entries == null || saveData.Entries.Count == 0)
            {
                Log(LogLevel.Debug, "Arquivo de save vazio — inventário vazio.");
                return;
            }

            // CORREÇÃO: limpa antes de restaurar para evitar duplicatas
            _inventory.Clear();

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


    // ── Helper ────────────────────────────────────────────────────────────

    private static void Log(LogLevel level, string msg) =>
        LoggerService.PrintLogMessage(level, $"[InventorySave] {msg}", LogCategory.Inventory);

    // ── Custom Editor (somente no Editor) ────────────────────────────────

#if UNITY_EDITOR
    [UnityEditor.CustomEditor(typeof(PlayerInventorySaveSystem))]
    private class PlayerInventorySaveSystemEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var system = (PlayerInventorySaveSystem)target;

            // Botões só fazem sentido em runtime — FileService e _saveDirectory
            // só estão inicializados após o Awake.
            UnityEditor.EditorGUILayout.Space(8);
            UnityEditor.EditorGUILayout.LabelField("Runtime Controls", UnityEditor.EditorStyles.boldLabel);

            UnityEngine.GUI.enabled = Application.isPlaying;

            if (GUILayout.Button("Save"))
                system.SaveAsync();

            if (GUILayout.Button("Load"))
                _ = system.LoadAsync();

            UnityEditor.EditorGUILayout.Space(4);

            // Vermelho para sinalizar que Clear é destrutivo
            var prevColor = UnityEngine.GUI.backgroundColor;
            UnityEngine.GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);

            if (GUILayout.Button("Clear Save"))
                _ = system.ClearSave();

            UnityEngine.GUI.backgroundColor = prevColor;
            UnityEngine.GUI.enabled = true;

            if (!Application.isPlaying)
                UnityEditor.EditorGUILayout.HelpBox(
                    "Entre em Play Mode para usar os controles acima.",
                    UnityEditor.MessageType.Info);
        }
    }
#endif
}