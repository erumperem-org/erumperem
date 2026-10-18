using Core.Exploration.Items;
using Erumperem.Characters;

namespace Core.Inventory.UI.ItemUse
{
    /// <summary>Immutable snapshot of a completed use (the context itself is cleared right after).</summary>
    public readonly struct ItemUseResult
    {
        public InventorySystem Source { get; }
        public int SlotIndex { get; }
        public IIITem Item { get; }

        /// <summary>Null for items that need no target character.</summary>
        public AllyCharacterStatDefinition Target { get; }

        public ItemUseResult(InventorySystem source, int slotIndex, IIITem item, AllyCharacterStatDefinition target)
        {
            Source = source;
            SlotIndex = slotIndex;
            Item = item;
            Target = target;
        }
    }
}
