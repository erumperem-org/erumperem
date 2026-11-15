using Core.Storage;

namespace Core.Rewards.Distribution
{
    /// <summary>
    /// Outcome of delivering ONE reward entry (one storageable + its rolled amount).
    /// Pure data: produced by the distribution layer, consumed by the UI layer.
    /// </summary>
    public readonly struct RewardLine
    {
        public InterfaceStorageable Storageable { get; }
        public int Requested { get; }
        public int Accepted { get; }

        /// <summary>Units that had nowhere to go (no wallet, no room in either inventory).</summary>
        public int Rejected => Requested - Accepted;

        /// <summary>True when at least one unit was lost. The UI paints the element red.</summary>
        public bool HasRejection => Rejected > 0;

        public RewardLine(InterfaceStorageable storageable, int requested, int accepted)
        {
            Storageable = storageable;
            Requested = requested;
            Accepted = accepted < 0 ? 0 : (accepted > requested ? requested : accepted);
        }
    }
}
