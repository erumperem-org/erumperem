using System;
using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;

public sealed class PlayerInventorySystem : MonoBehaviour
{
    private readonly Dictionary<IStorageable, int> _inventory = new();

    public event Action<IStorageable, int> OnItemAdded;
    public event Action<IStorageable, int> OnItemRemoved;

    /// <summary>
    /// Retorna todos os itens e suas quantidades no inventário.
    /// Usado pelo InventoryPanelView para popular a UI no OnEnable.
    /// </summary>
    public IEnumerable<KeyValuePair<IStorageable, int>> GetAll() => _inventory;

    // ── API pública ───────────────────────────────────────────────────────

    public void AddItems(Dictionary<IStorageable, int> items)
    {
        if (!ValidateCollection(items, "add")) return;

        foreach ((IStorageable item, int amount) in items)
        {
            if (!ValidateItem(item, "add")) continue;

            Log(LogLevel.Debug, $"Trying to add [{item}] mode=[{item.storageMode}] amount=[{amount}]");

            // FIX 1: removida chamada duplicada de OnItemAdded.Invoke() aqui.
            // Os métodos privados (AddSingleSlot, AddUnique, AddStackable)
            // já disparam o evento internamente — invocar aqui causava duplo disparo.
            AddItem(item, amount);
        }

        PrintDebug();
    }

    public void RemoveItems(Dictionary<IStorageable, int> items)
    {
        if (!ValidateCollection(items, "remove")) return;

        foreach ((IStorageable item, int amount) in items)
        {
            if (!ValidateItem(item, "remove")) continue;

            // FIX 2: guarda contra amount inválido — sem isso remoções
            // com amount <= 0 chegavam silenciosamente até RemoveFromStack
            // e corrompiam o stack sem disparar warning.
            if (amount <= 0)
            {
                Log(LogLevel.Warning, $"Attempted to remove invalid amount [{amount}] from [{item}]");
                continue;
            }

            if (!_inventory.ContainsKey(item))
            {
                Log(LogLevel.Warning, $"Attempted to remove non-existing item [{item}]");
                continue;
            }

            Log(LogLevel.Debug, $"Trying to remove [{amount}] from [{item}]");
            RemoveItem(item, amount);
        }

        PrintDebug();
    }

    public void RemoveItem(IStorageable item)
    {
        if (!ValidateItem(item, "remove")) return;

        if (!_inventory.ContainsKey(item))
        {
            Log(LogLevel.Warning, $"Attempted to remove non-existing item [{item}]");
            return;
        }

        Log(LogLevel.Debug, $"Trying to remove [1] from [{item}]");
        RemoveItem(item, 1);

        PrintDebug();
    }

    public bool Contains(IStorageable item) => _inventory.ContainsKey(item);

    public int GetAmount(IStorageable item) =>
        _inventory.TryGetValue(item, out int amount) ? amount : 0;

    // ── Add por StorageMode ───────────────────────────────────────────────

    private void AddItem(IStorageable item, int amount)
    {
        switch (item.storageMode)
        {
            case StorageMode.SingleSlot:
                AddSingleSlot(item);
                break;

            case StorageMode.Unique:
                AddUnique(item);
                break;

            case StorageMode.Stackable:
            case StorageMode.Unlimited:
                AddStackable(item, amount);
                break;

            default:
                Log(LogLevel.Error, $"Unsupported storage mode [{item.storageMode}] for [{item}]");
                break;
        }
    }

    private void AddSingleSlot(IStorageable item)
    {
        _inventory[item] = 1;
        Log(LogLevel.Debug, $"Added SingleSlot [{item}]");
        OnItemAdded?.Invoke(item, 1);
    }

    private void AddUnique(IStorageable item)
    {
        if (_inventory.ContainsKey(item))
        {
            Log(LogLevel.Warning, $"Unique item [{item}] already exists — skipped");
            return;
        }

        _inventory[item] = 1;
        Log(LogLevel.Debug, $"Added Unique [{item}]");
        OnItemAdded?.Invoke(item, 1);
    }

    private void AddStackable(IStorageable item, int amount)
    {
        if (_inventory.ContainsKey(item))
        {
            _inventory[item] += amount;
            Log(LogLevel.Debug, $"Stacked [{item}] +{amount} → total [{_inventory[item]}]");
        }
        else
        {
            _inventory[item] = amount;
            Log(LogLevel.Debug, $"Created stack [{item}] amount=[{amount}]");
        }

        OnItemAdded?.Invoke(item, amount);
    }

    // ── Remove por StorageMode ────────────────────────────────────────────

    private void RemoveItem(IStorageable item, int amount)
    {
        switch (item.storageMode)
        {
            case StorageMode.SingleSlot:
            case StorageMode.Unique:
                RemoveCompletely(item);
                break;

            case StorageMode.Stackable:
            case StorageMode.Unlimited:
                RemoveFromStack(item, amount);
                break;

            default:
                Log(LogLevel.Error, $"Unsupported remove mode [{item.storageMode}] for [{item}]");
                break;
        }
    }

    private void RemoveCompletely(IStorageable item)
    {
        _inventory.Remove(item);
        Log(LogLevel.Debug, $"Removed [{item}] completely");
        OnItemRemoved?.Invoke(item, 1);
    }
    /// <summary>Remove todos os itens do inventário em memória e dispara OnItemRemoved para cada um.</summary>
    public void Clear()
    {
        if (_inventory.Count == 0) return;

        // Copia as entradas antes de iterar pois vamos modificar o dicionário
        var entries = new List<KeyValuePair<IStorageable, int>>(_inventory);

        foreach (var (item, amount) in entries)
        {
            _inventory.Remove(item);
            OnItemRemoved?.Invoke(item, amount);
            Log(LogLevel.Debug, $"Clear: removido [{item}] amount=[{amount}]");
        }

        Log(LogLevel.Debug, "Inventário limpo (Clear).");
        PrintDebug();
    }
    private void RemoveFromStack(IStorageable item, int amount)
    {
        // FIX 3a: clamp para evitar stack negativo quando amount > quantidade atual.
        // Sem isso _inventory[item] ficava negativo e o evento reportava
        // uma quantidade removida maior do que a que existia.
        int actual = Mathf.Min(amount, _inventory[item]);
        _inventory[item] -= actual;

        Log(LogLevel.Debug, $"Decreased [{item}] -{actual} → remaining [{_inventory[item]}]");

        if (_inventory[item] <= 0)
        {
            _inventory.Remove(item);
            Log(LogLevel.Debug, $"Removed empty stack [{item}]");
        }

        // FIX 3b: evento consolidado fora do if/else — antes estava duplicado
        // dentro de cada branch, o que tornava difícil adicionar lógica futura
        // sem disparar em duplicata acidentalmente.
        OnItemRemoved?.Invoke(item, actual);
    }

    // ── Debug ─────────────────────────────────────────────────────────────

    public void PrintDebug()
    {
        Log(LogLevel.Debug, "================ INVENTORY DEBUG ================");

        if (_inventory.Count == 0)
        {
            Log(LogLevel.Debug, "Inventory is empty");
            return;
        }

        int index = 0;
        foreach ((IStorageable item, int amount) in _inventory)
        {
            if (item == null)
                Log(LogLevel.Warning, $"Slot [{index}] contains NULL item");
            else
                Log(LogLevel.Debug, $"Slot [{index}] | [{item}] | Mode [{item.storageMode}] | Amount [{amount}]");

            index++;
        }

        Log(LogLevel.Debug, $"Total Unique Entries: [{_inventory.Count}]");
        Log(LogLevel.Debug, "=================================================");
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private bool ValidateCollection(Dictionary<IStorageable, int> items, string operation)
    {
        if (items != null && items.Count > 0) return true;
        Log(LogLevel.Warning, $"Attempted to {operation} NULL or empty collection");
        PrintDebug();
        return false;
    }

    private bool ValidateItem(IStorageable item, string operation)
    {
        if (item != null) return true;
        Log(LogLevel.Warning, $"Attempted to {operation} NULL item");
        return false;
    }

    private static void Log(LogLevel level, string message) =>
        LoggerService.PrintLogMessage(level, $"[Inventory] {message}", LogCategory.Inventory);

    // ── Custom Editor (somente no Editor) ────────────────────────────────

#if UNITY_EDITOR
    /// <summary>
    /// Exibe o Dictionary<IStorageable, int> no Inspector já que dicionários
    /// não são serializáveis pelo Unity por padrão.
    /// </summary>
    [UnityEditor.CustomEditor(typeof(PlayerInventorySystem))]
    private class PlayerInventorySystemEditor : UnityEditor.Editor
    {
        private bool _foldout = true;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var system = (PlayerInventorySystem)target;

            UnityEditor.EditorGUILayout.Space(8);
            _foldout = UnityEditor.EditorGUILayout.Foldout(_foldout, $"Inventory ({system._inventory.Count} entries)", true);

            if (!_foldout) return;

            if (system._inventory.Count == 0)
            {
                UnityEditor.EditorGUILayout.HelpBox("Inventory is empty.", UnityEditor.MessageType.Info);
                return;
            }

            UnityEditor.EditorGUI.indentLevel++;

            int index = 0;
            foreach ((IStorageable item, int amount) in system._inventory)
            {
                string label = item != null
                    ? $"[{index}] {item.GetType().Name}  |  Mode: {item.storageMode}  |  Amount: {amount}"
                    : $"[{index}] NULL item";

                UnityEditor.EditorGUILayout.LabelField(label);
                index++;
            }

            UnityEditor.EditorGUI.indentLevel--;

            UnityEditor.EditorGUILayout.Space(4);
            if (GUILayout.Button("Print Debug to Console"))
                system.PrintDebug();
        }
    }
#endif
}