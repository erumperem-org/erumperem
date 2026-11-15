using System.Collections.Generic;
using Core.Storage;
using Services.DebugUtilities;

namespace Core.Rewards.Distribution
{
    /// <summary>
    /// Plain C# service: hands each rolled reward to the first receiver that
    /// accepts its type, then asks every receiver to persist. Knows nothing
    /// about wallets, inventories or UI.
    /// </summary>
    public sealed class RewardDistributionService
    {
        private readonly IReadOnlyList<IRewardReceiver> _receivers;

        public RewardDistributionService(IReadOnlyList<IRewardReceiver> receivers)
        {
            _receivers = receivers ?? new List<IRewardReceiver>();
        }

        public IReadOnlyList<RewardLine> Distribute(IReadOnlyDictionary<InterfaceStorageable, int> rewards)
        {
            var lines = new List<RewardLine>();
            if (rewards == null || rewards.Count == 0) return lines;

            foreach (var (storageable, amount) in rewards)
            {
                var receiver = FindReceiver(storageable);

                if (receiver == null)
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[RewardDistributionService] No receiver for '{storageable?.StorageableId}'.",
                        LogCategory.Inventory);
                    lines.Add(new RewardLine(storageable, amount, 0));
                    continue;
                }

                lines.Add(receiver.Receive(storageable, amount));
            }

            foreach (var receiver in _receivers)
                receiver.Commit();

            return lines;
        }

        private IRewardReceiver FindReceiver(InterfaceStorageable storageable)
        {
            if (storageable == null) return null;

            foreach (var receiver in _receivers)
                if (receiver.CanReceive(storageable))
                    return receiver;

            return null;
        }
    }
}
