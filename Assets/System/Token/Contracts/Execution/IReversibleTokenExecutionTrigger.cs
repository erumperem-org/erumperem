using System;

// EXECUTION TRIGGER
// Defines how the token behaves after being applied.

namespace Core.Tokens
{
    // Apply once, then reversed on Token deactivation.
    public interface IReversibleTokenExecutionTrigger : ITokenExecutionTrigger { void ReverseTokenEffect(); }
}
