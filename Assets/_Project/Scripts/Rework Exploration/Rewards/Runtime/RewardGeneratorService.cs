using System.Collections.Generic;
using Core.Storage;

namespace Core.Rewards
{
    /// <summary>
    /// Service reusable by multiple systems (chests, dispensers, etc.) to
    /// roll rewards from a LootTable. New, parallel structure — does not
    /// share implementation with the original LootService.
    /// </summary>
    public sealed class RewardGeneratorService
    {
        private readonly IDrawMethod _drawMethod;

        public RewardGeneratorService(IDrawMethod drawMethod = null)
        {
            _drawMethod = drawMethod ?? new IndependentChanceDrawMethod();
        }

        public IReadOnlyDictionary<InterfaceStorageable, int> Generate(LootTable table)
        {
            if (table == null || table.Entries.Count == 0)
                return new Dictionary<InterfaceStorageable, int>();

            return _drawMethod.Draw(table.Entries);
        }
    }
}
