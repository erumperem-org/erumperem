using System.Collections.Generic;
using Core.Storage;

namespace Core.Rewards
{
    /// <summary>
    /// Contract for a draw algorithm over a LootTable. Kept as an interface
    /// to allow future draw modes without changing RewardGeneratorService
    /// or LootTable.
    /// </summary>
    public interface IDrawMethod
    {
        IReadOnlyDictionary<InterfaceStorageable, int> Draw(IReadOnlyList<LootEntry> entries);
    }
}
