using System;
using System.Collections.Generic;

namespace Core.Shop
{
    [Serializable]
    public sealed class RotatingShopSaveData
    {
        public List<Entry> Entries = new();

        [Serializable]
        public sealed class Entry
        {
            public string Id;
            public int Index;
        }
    }
}
