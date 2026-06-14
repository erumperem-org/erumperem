using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;
/// <summary>
/// ScriptableObject que mapeia <c>ItemId</c> → <c>IStorageable</c>.
/// Necessário para o <see cref="PlayerInventorySaveSystem"/> resolver os
/// ids persistidos de volta aos objetos em runtime.
///
/// Crie via: Assets → Create → Inventory → Item Registry
/// Arraste todos os IItem do projeto para a lista <c>Items</c>.
/// </summary>
[CreateAssetMenu(menuName = "Inventory/Item Registry", fileName = "ItemRegistry")]
public sealed class ItemRegistry : ScriptableObject
{
    [Tooltip("Todos os itens do projeto. O ItemId de cada um deve ser único.")]
    [SerializeField] private List<ScriptableObject> _items = new();

    private Dictionary<string, IStorageable> _lookup;

    private void OnEnable() => BuildLookup();

    /// <summary>Resolve um <c>ItemId</c> para o <see cref="IStorageable"/> correspondente.</summary>
    public IStorageable Resolve(string itemId)
    {
        if (_lookup == null) BuildLookup();
        return _lookup.TryGetValue(itemId, out var item) ? item : null;
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, IStorageable>(_items.Count);
        foreach (var item in _items)
        {
            if (item is IItem item1)
            {
                if (item == null) continue;

                if (_lookup.ContainsKey(item1.ItemId))
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[ItemRegistry] ItemId duplicado detectado: '{item1.ItemId}' — entrada ignorada.",
                        LogCategory.Inventory);
                    continue;
                }

                _lookup[item1.ItemId] = item1;
            }
        }
    }
}
