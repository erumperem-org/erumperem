using UnityEngine;
using Core.Exploration.Items;

namespace Core.Inventory
{
    /// <summary>
    /// Data for an insertion/removal event. Raised once per distinct item
    /// type affected by an operation (bulk adds/removes raise one event
    /// per distinct item, not one per slot).
    /// </summary>
    public readonly struct InventorySlotChangedEventArgs
    {
        public readonly IIITem Item;
        public readonly int Quantity;
        public readonly Color InventoryColor;
        public readonly InventoryChangeType ChangeType;

        public InventorySlotChangedEventArgs(IIITem item, int quantity, Color inventoryColor, InventoryChangeType changeType)
        {
            Item = item;
            Quantity = quantity;
            InventoryColor = inventoryColor;
            ChangeType = changeType;
        }
    }
}
