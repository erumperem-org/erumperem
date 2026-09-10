using System.Collections.Generic;
using UnityEngine;
using Core.Storage;

namespace Core.Rewards
{
    /// <summary>
    /// Each entry is evaluated in isolation against its own chance (%).
    /// There is no "total rolls" replacement mechanic: any subset of the
    /// entries can come out, including none or all of them. No pity/guarantee.
    /// </summary>
    public sealed class IndependentChanceDrawMethod : IDrawMethod
    {
        public IReadOnlyDictionary<InterfaceStorageable, int> Draw(IReadOnlyList<LootEntry> entries)
        {
            var result = new Dictionary<InterfaceStorageable, int>();

            foreach (var entry in entries)
            {
                if (!entry.IsValid) continue;

                float roll = Random.Range(0f, 100f);
                if (roll > entry.ChancePercent) continue;

                int quantity = Random.Range(entry.MinQuantity, entry.MaxQuantity + 1); // int Range is max-exclusive
                var storageable = entry.Storageable;

                result.TryGetValue(storageable, out int current);
                result[storageable] = current + quantity;
            }

            return result;
        }
    }
}
