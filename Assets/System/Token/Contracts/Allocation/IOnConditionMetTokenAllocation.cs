using System;
using Services.DebugUtilities.Console;

// ALLOCATION
// Defines when the token is initially applied.

namespace Core.Tokens
{
    // Applied only if a predefined condition is satisfied. (OnHit + Conditioning)
    public sealed class IOnConditionMetTokenAllocation : ITokenAllocationStyle
    {
        public Func<bool> AllocationCondition;
        public IOnConditionMetTokenAllocation(Func<bool> condition)
        {
            this.AllocationCondition = condition;
        }
    }
}
