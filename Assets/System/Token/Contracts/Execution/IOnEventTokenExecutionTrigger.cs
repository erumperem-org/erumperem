using System;

// EXECUTION TRIGGER
// Defines how the token behaves after being applied.

namespace Core.Tokens
{
    // Activates only when a specific event happens.
    public interface IOnEventTokenExecutionTrigger : ITokenExecutionTrigger
    {
        Type EventTriggerType { get; }
        void Subscribe();
    }
}
