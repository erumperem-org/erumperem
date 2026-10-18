using Core.CharacterStats;
using Core.Exploration.Items;
using Erumperem.Characters;

namespace Core.Inventory.UI.ItemUse
{
    /// <summary>
    /// Accumulator filled step by step by the item-use flow: first WHAT and
    /// WHERE (item + exact inventory + exact slot), then, only if the item
    /// needs one, WHO (target character). Whatever is confirmed at the end is
    /// built from this object alone.
    /// </summary>
    public sealed class ItemUseContext
    {
        /// <summary>The exact inventory the item was selected from (losable or permanent).</summary>
        public InventorySystem Source { get; private set; }
        public int SlotIndex { get; private set; } = -1;
        public IIITem Item { get; private set; }
        public AllyCharacterStatDefinition Target { get; private set; }

        public bool HasItem => Item != null && Source != null && SlotIndex >= 0;
        public bool HasTarget => Target != null;

        /// <summary>The "extra layer" view of the item, or null for items that need no target.</summary>
        public ICharacterTargetedItem TargetedItem => Item as ICharacterTargetedItem;

        public bool RequiresTarget => TargetedItem != null;

        public bool IsReadyToConfirm => HasItem && (!RequiresTarget || HasTarget);

        /// <summary>Selecting an item always discards any previously chosen target.</summary>
        public void SetItem(InventorySystem source, int slotIndex, IIITem item)
        {
            Source = source;
            SlotIndex = slotIndex;
            Item = item;
            Target = null;
        }

        public void SetTarget(AllyCharacterStatDefinition target) => Target = target;

        public void ClearTarget() => Target = null;

        public void Clear()
        {
            Source = null;
            SlotIndex = -1;
            Item = null;
            Target = null;
        }
    }
}
