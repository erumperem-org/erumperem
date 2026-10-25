using System;
using System.Collections.Generic;
using System.IO;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Shop
{
    /// <summary>
    /// Orquestrador central do estado dos RotatingItemShopButton. Mantém um único
    /// JSON (id -> índice) dentro da pasta do slot ativo, lida via SaveSlotAccess.Data.
    /// Salva a cada mudança e recarrega automaticamente quando o slot muda.
    ///
    /// Coloque-o no mesmo GameObject raiz do SaveSlotAccess (ou em outro objeto raiz):
    /// usa DontDestroyOnLoad e é singleton.
    /// </summary>
    public sealed class RotatingShopStateRegistry : MonoBehaviour
    {
        public static RotatingShopStateRegistry Instance { get; private set; }

        /// <summary>Disparado após carregar o estado de um slot (ou limpá-lo). Botões devem reler seu índice.</summary>
        public static event Action OnStateLoaded;

        [SerializeField] private string _fileName = "rotating_shop.json";

        private readonly Dictionary<string, int> _states = new();
        private CurrentSlotData _hookedData;

        private string FullPath =>
            _hookedData != null && _hookedData.HasSlotSelected
                ? Path.Combine(_hookedData.SlotDirectory, _fileName)
                : null;

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            TryHook();
        }

        // SaveSlotAccess pode ter inicializado depois do nosso Awake.
        private void Start() => TryHook();

        private void OnDestroy()
        {
            if (_hookedData != null) _hookedData.OnSlotChanged -= HandleSlotChanged;
            if (Instance == this) Instance = null;
        }

        // ── Public API ─────────────────────────────────────────────────────

        public bool TryGetIndex(string id, out int index) => _states.TryGetValue(id, out index);

        public void SetIndex(string id, int index)
        {
            _states[id] = index;
            Save();
        }

        // ── Internals ──────────────────────────────────────────────────────

        private void TryHook()
        {
            if (_hookedData != null) return;

            var data = SaveSlotAccess.Data;
            if (data == null) return;

            _hookedData = data;
            _hookedData.OnSlotChanged += HandleSlotChanged;

            if (_hookedData.HasSlotSelected) Load();
        }

        private void HandleSlotChanged(int slotIndex, string slotDirectory) => Load();

        private void Load()
        {
            _states.Clear();

            string path = FullPath;
            if (path != null && File.Exists(path))
            {
                try
                {
                    var data = JsonUtility.FromJson<RotatingShopSaveData>(File.ReadAllText(path));
                    if (data?.Entries != null)
                        foreach (var e in data.Entries)
                            if (!string.IsNullOrEmpty(e.Id)) _states[e.Id] = e.Index;

                    Log(LogLevel.Debug, $"Loaded {_states.Count} rotating shop state(s) from '{path}'.");
                }
                catch (Exception ex)
                {
                    Log(LogLevel.Error, $"Failed to load: {ex.Message}");
                }
            }

            OnStateLoaded?.Invoke();
        }

        private void Save()
        {
            string path = FullPath;
            if (path == null)
            {
                Log(LogLevel.Warning, "No slot selected — state kept in memory only, not saved.");
                return;
            }

            var data = new RotatingShopSaveData();
            foreach (var kv in _states)
                data.Entries.Add(new RotatingShopSaveData.Entry { Id = kv.Key, Index = kv.Value });

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                // Escrita via arquivo temporário para não corromper o save em caso de falha no meio.
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to save: {ex.Message}");
            }
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[RotatingShopStateRegistry] {msg}", LogCategory.Inventory);
    }
}
