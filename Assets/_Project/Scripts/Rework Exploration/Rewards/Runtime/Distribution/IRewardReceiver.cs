using Core.Storage;

namespace Core.Rewards.Distribution
{
    /// <summary>
    /// A destination for rewards (wallet, inventories, ...). Each receiver
    /// owns the rules for ONE kind of storageable. New destinations are added
    /// by implementing this interface — RewardDistributionService never changes.
    /// </summary>
    public interface IRewardReceiver
    {
        bool CanReceive(InterfaceStorageable storageable);

        /// <summary>Delivers as much as possible. Never throws for "no room" — reports it in the line.</summary>
        RewardLine Receive(InterfaceStorageable storageable, int amount);

        /// <summary>
        /// Called once after the whole batch was delivered. Persists whatever
        /// this receiver changed (no-op if nothing changed).
        /// </summary>
        void Commit();
    }
}
