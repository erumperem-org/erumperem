using System.Collections.Generic;
using UnityEngine;

namespace Core.Rewards
{
    [CreateAssetMenu(menuName = "Rewards/Loot Table", fileName = "LootTable")]
    public sealed class LootTable : ScriptableObject
    {
        [SerializeField] private List<LootEntry> _entries = new();

        public IReadOnlyList<LootEntry> Entries => _entries;
    }
}
