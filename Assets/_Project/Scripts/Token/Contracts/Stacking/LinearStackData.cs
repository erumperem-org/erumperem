using System;
using Unity.VisualScripting;

// STACKING MODEL
// Defines how multiple instances of the same token behave.

namespace Core.Tokens
{
    // Increases stack count linearly; each additional stack provides a proportional bonus.
    [Serializable]
    public sealed class LinearStackData : TokenStackData
    {
        public float increaseFactor;
        public int stacks = 1;

        public LinearStackData(float increaseFactor, int initialStacks = 1)
        {
            this.increaseFactor = increaseFactor;
            this.stacks = initialStacks;
        }
    }
}
