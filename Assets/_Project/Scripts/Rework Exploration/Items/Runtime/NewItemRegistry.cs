using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Exploration.Items
{
    /// <summary>
    /// ScriptableObject que mapeia StorageableId → IIITem. Necessário para
    /// sistemas de save (inventário, loja, etc.) resolverem os ids persistidos
    /// de volta aos objetos em runtime.
    ///
    /// Foco exclusivo em itens (IIITem) — moedas (ICoin) possuem seu próprio
    /// registry (CoinRegistry), separados por SRP.
    ///
    /// Crie via: Assets → Create → Inventory → Item Registry
    /// Arraste todos os IIITem do projeto para a lista <c>Items</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Item Registry", fileName = "ItemRegistry")]
    public sealed class NewItemRegistry : ScriptableObject
    {
        [Tooltip("Todos os itens do projeto. O StorageableId de cada um deve ser único.")]
        [SerializeField] private List<ScriptableObject> _items = new();

        private Dictionary<string, IIITem> _lookup;

        /// <summary>
        /// Exposto somente para leitura por ferramentas externas de validação
        /// (ex: ItemRegistryValidator), sem acoplar este SO a UnityEditor.
        /// </summary>
        public IReadOnlyList<ScriptableObject> Items => _items;

        private void OnEnable() => BuildLookup();

        /// <summary>Resolve um StorageableId para o IIITem correspondente.</summary>
        public IIITem Resolve(string storageableId)
        {
            if (_lookup == null) BuildLookup();
            return _lookup.TryGetValue(storageableId, out var item) ? item : null;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, IIITem>(_items.Count);
            foreach (var obj in _items)
            {
                if (obj is not IIITem item) continue;

                string id = item.StorageableId;

                if (string.IsNullOrEmpty(id))
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[ItemRegistry] '{obj.name}' tem StorageableId vazio — ignorado.",
                        LogCategory.Inventory);
                    continue;
                }

                if (_lookup.ContainsKey(id))
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[ItemRegistry] StorageableId duplicado: '{id}' ({obj.name}) — ignorado.",
                        LogCategory.Inventory);
                    continue;
                }

                _lookup[id] = item;
            }
        }
    }
}
