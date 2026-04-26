using System;
using Unity.VisualScripting;

// STACKING MODEL
// Defines how multiple instances of the same token behave.

namespace Core.Tokens
{
    // Increases stacks with diminishing returns.
    [Serializable]
    public sealed class DiminishingStackData : TokenStackData
    {
        public float decreaseFactor;
        public int stacks = 1;

        public DiminishingStackData(float decreaseFactor, int initialStacks = 1)
        {
            this.decreaseFactor = decreaseFactor;
            this.stacks = initialStacks;
        }
    }
}
