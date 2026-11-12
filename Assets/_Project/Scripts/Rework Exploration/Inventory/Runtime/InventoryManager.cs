using System.Collections.Generic;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;

namespace Core.Inventory
{
    /// <summary>
    /// Orchestrates the relationship between the losable and the permanent
    /// inventory: full migration on demand (external specific event — see
    /// <see cref="RequestFullMigration"/>) and auto-refill of the permanent
    /// inventory whenever one of its slots empties out.
    ///
    /// Auto-refill only fills the slot(s) that JUST became empty. Removing a
    /// unit from a stack that still has units left does not trigger it, and
    /// slots that were already empty are left alone.
    /// </summary>
    public sealed class InventoryManager : MonoBehaviour
    {
        [SerializeField] private InventorySystem _losable;
        [SerializeField] private InventorySystem _permanent;
        public InventorySaveSystem losable, permanent;

        // Which permanent slots were empty after the last event (to detect slots that just emptied).
        private bool[] _permanentEmpty;

        // True enquanto uma limpeza está em andamento: o esvaziamento é intencional e não deve disparar auto-refill.
        private bool _suppressAutoRefill;

        private void OnEnable()
        {
            if (_permanent == null) return;

            _permanent.OnInventoryChanged += HandlePermanentChanged;
            SnapshotPermanent();
        }

        // InventorySystem allocates its slots in Awake; Start is guaranteed to run after it.
        private void Start() => SnapshotPermanent();

        private void OnDisable()
        {
            if (_permanent != null)
                _permanent.OnInventoryChanged -= HandlePermanentChanged;
        }

        /// <summary>
        /// Entry point for the external specific event that authorizes the
        /// losable → permanent migration. Should be called by whatever
        /// system raises that event (outside the inventory's scope).
        /// </summary>
        public void RequestFullMigration()
        {
            if (_losable == null || _permanent == null) return;

            // Snapshot by index — migration priority is sequential slot order in the losable inventory.
            var slots = _losable.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty) continue;

                int moved = _permanent.AddAsMuchAsPossible(slot.Item, slot.Quantity);
                if (moved > 0)
                    _losable.TryRemoveItem(slot.Item, moved);
            }

            Log(LogLevel.Debug, "Losable → permanent migration completed.");
        }

        /// <summary>
        /// Remove todos os itens do inventário indicado. Retorna o total de unidades removidas.
        /// Ao limpar o permanente, o auto-refill é suprimido (o inventário fica vazio de fato).
        /// </summary>
        public int ClearInventory(InventoryType type)
        {
            InventorySystem target = type switch
            {
                InventoryType.Losable   => _losable,
                InventoryType.Permanent => _permanent,
                _                       => null
            };

            if (target == null)
            {
                Log(LogLevel.Warning, $"ClearInventory: inventário '{type}' não está atribuído.");
                return 0;
            }

            int totalRemoved = 0;
            _suppressAutoRefill = true;
            try
            {
                var slots = target.Slots;
                for (int i = 0; i < slots.Count; i++)
                {
                    // Copia item/quantidade antes de remover (slots pode ser struct).
                    if (!target.TryGetSlot(i, out IIITem item, out int quantity)) continue;

                    totalRemoved += target.TryRemoveFromSlot(i, item, quantity);
                }
            }
            finally
            {
                _suppressAutoRefill = false;
            }

            // Garante que o snapshot reflita o estado final, caso o alvo seja o permanente.
            if (type == InventoryType.Permanent)
                SnapshotPermanent();

            Log(LogLevel.Debug, $"Inventário '{type}' limpo: {totalRemoved} unidade(s) removida(s).");
            permanent.SaveAsync();
            return totalRemoved;
        }

        private void HandlePermanentChanged(InventorySlotChangedEventArgs args)
        {
            // Limpeza intencional em andamento: só mantém o snapshot sincronizado, sem auto-refill.
            if (_suppressAutoRefill)
            {
                SnapshotPermanent();
                return;
            }

            // Transferência em andamento (TransferTo): o esvaziamento do slot é
            // intencional e não deve disparar auto-refill. Apenas mantém o snapshot
            // sincronizado para que o diff do próximo evento normal seja correto.
            if (_permanent.IsTransferInProgress)
            {
                SnapshotPermanent();
                return;
            }

            // Size changed (or slots not allocated yet when we subscribed): resync, nothing to diff.
            if (_permanentEmpty == null || _permanentEmpty.Length != _permanent.Size)
            {
                SnapshotPermanent();
                return;
            }

            List<int> justEmptied = args.ChangeType == InventoryChangeType.Removed
                ? CollectJustEmptiedSlots()
                : null;

            SnapshotPermanent(); // before refilling: the refill's own events keep the snapshot current

            if (justEmptied == null || justEmptied.Count == 0) return;

            RefillSlots(justEmptied);
        }

        private List<int> CollectJustEmptiedSlots()
        {
            var result = new List<int>();
            var slots = _permanent.Slots;

            for (int i = 0; i < _permanentEmpty.Length; i++)
                if (!_permanentEmpty[i] && slots[i].IsEmpty)
                    result.Add(i);

            return result;
        }

        private void RefillSlots(List<int> slotIndices)
        {
            if (_losable == null) return;

            foreach (int slotIndex in slotIndices)
            {
                if (!_losable.TryGetFirstOccupiedSlot(out IIITem item, out int available))
                    break; // losable inventory is empty, nothing left to pull

                int moved = _permanent.TryFillSpecificSlot(slotIndex, item, available);
                if (moved > 0)
                    _losable.TryRemoveItem(item, moved);
            }
        }

        private void SnapshotPermanent()
        {
            if (_permanent == null) return;

            int size = _permanent.Size;
            if (_permanentEmpty == null || _permanentEmpty.Length != size)
                _permanentEmpty = new bool[size];

            if (size == 0) return;

            var slots = _permanent.Slots;
            for (int i = 0; i < size; i++)
                _permanentEmpty[i] = slots[i].IsEmpty;
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[InventoryManager:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}

namespace Core.Inventory
{
    /// <summary>Identifica qual dos inventários gerenciados pelo InventoryManager é o alvo de uma operação.</summary>
    public enum InventoryType
    {
        Losable,
        Permanent
    }
}