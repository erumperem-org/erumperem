using Core.Exploration.Items;

namespace Core.Inventory
{
    /// <summary>
    /// Storage unit of the inventory. Empty when <see cref="Item"/> is null.
    /// </summary>
    public struct InventorySlot
    {
        public IIITem Item;
        public int Quantity;

        public bool IsEmpty => Item == null || Quantity <= 0;

        public void Clear()
        {
            Item = null;
            Quantity = 0;
        }
    }
}
