using System;
using Services.DebugUtilities.Console;

// ALLOCATION
// Defines when the token is initially applied.

namespace Core.Tokens
{
    // Applied when a specific event is triggered.
    public sealed class IOnEventTokenAllocation : ITokenAllocationStyle
    {
        public delegate void SubscribeToEvent(Type eventType);
        public SubscribeToEvent OnSubscribe;

        public IOnEventTokenAllocation(SubscribeToEvent subscribeCallback)
        {
            this.OnSubscribe = subscribeCallback;
        }
    }
}
