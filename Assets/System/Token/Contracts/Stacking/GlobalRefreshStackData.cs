using System;
using Unity.VisualScripting;

// STACKING MODEL
// Defines how multiple instances of the same token behave.

namespace Core.Tokens
{
    // Increases stacks and refreshes duration on all tokens of the same type.
    [Serializable]
    public sealed class GlobalRefreshStackData : TokenStackData
    {
        public int duration;
        public int stacks = 1;

        public GlobalRefreshStackData(int duration, int initialStacks = 1)
        {
            this.duration = duration;
            this.stacks = initialStacks;
        }
    }
}
