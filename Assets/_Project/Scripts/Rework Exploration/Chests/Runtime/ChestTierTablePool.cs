using System;
using System.Collections.Generic;
using UnityEngine;
using Core.Rewards;

namespace Core.Chests
{
    /// <summary>
    /// A tier can offer more than one possible LootTable for a chest.
    /// One table is picked at random from the pool each time loot is
    /// (re)assigned to a chest — giving content variety among chests
    /// that share the same tier, beyond the per-entry chance variance
    /// already provided by a single table.
    /// </summary>
    [Serializable]
    public sealed class ChestTierTablePool
    {
        [SerializeField] private List<LootTable> _tables = new();

        public IReadOnlyList<LootTable> Tables => _tables;

        public LootTable PickRandom()
        {
            if (_tables == null || _tables.Count == 0) return null;
            return _tables[UnityEngine.Random.Range(0, _tables.Count)];
        }
    }
}