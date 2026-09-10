using System;
using System.Collections.Generic;

namespace Core.Inventory
{
    [Serializable]
    public sealed class InventorySaveData
    {
        [Serializable]
        public struct SlotEntry
        {
            public string StorageableId;
            public int Quantity;
        }

        public int Size;
        public List<SlotEntry> Slots = new(); // empty slots are not included
    }
}
