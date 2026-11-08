namespace Core.CharacterStats
{
    /// <summary>Result of asking "can this item be applied to this character right now?".</summary>
    public readonly struct ItemApplicability
    {
        public bool CanApply { get; }

        /// <summary>Human-readable reason when <see cref="CanApply"/> is false; empty otherwise.</summary>
        public string Reason { get; }

        private ItemApplicability(bool canApply, string reason)
        {
            CanApply = canApply;
            Reason = reason ?? string.Empty;
        }

        public static ItemApplicability Allowed => new(true, string.Empty);
        public static ItemApplicability Denied(string reason) => new(false, reason);
    }
}
