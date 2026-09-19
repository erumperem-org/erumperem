using System;
using System.Collections.Generic;

namespace Core.CharacterStats
{
    /// <summary>
    /// Serializable shape of the single shared JSON file holding every
    /// character's stats. JsonUtility cannot serialize Dictionary directly,
    /// so both the character list and each character's stat list are
    /// stored as flat lists (same convention as WalletSaveData/InventorySaveData).
    /// </summary>
    [Serializable]
    public sealed class CharacterStatsFileData
    {
        [Serializable]
        public struct StatEntry
        {
            public StatType StatType;
            public float Value;
        }

        [Serializable]
        public struct CharacterEntry
        {
            public string CharacterId;
            public List<StatEntry> Stats;
        }

        public List<CharacterEntry> Characters = new();
    }
}
