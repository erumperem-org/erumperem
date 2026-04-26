using System;
using Unity.VisualScripting;

// STACKING MODEL
// Defines how multiple instances of the same token behave.

namespace Core.Tokens
{
    // Refreshes duration on reapplication rather than stacking.
    [Serializable]
    public sealed class RefreshDurationStackData : TokenStackData
    {
        public int currentDuration;
        public int maxDuration;

        public RefreshDurationStackData(int maxDuration)
        {
            this.maxDuration = maxDuration;
            this.currentDuration = maxDuration;
        }
    }
}
