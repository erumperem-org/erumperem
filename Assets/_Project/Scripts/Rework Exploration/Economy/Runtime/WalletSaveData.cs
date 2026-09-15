using System;
using System.Collections.Generic;

namespace Core.Economy.Currency
{
    [Serializable]
    public sealed class WalletSaveData
    {
        [Serializable]
        public struct CoinEntry
        {
            public string StorageableId;
            public int Amount;
        }

        public List<CoinEntry> Coins = new();
    }
}
