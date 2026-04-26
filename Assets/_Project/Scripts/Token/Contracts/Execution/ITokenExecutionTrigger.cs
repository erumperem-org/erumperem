using System;

// EXECUTION TRIGGER
// Defines how the token behaves after being applied.

namespace Core.Tokens
{
    public interface ITokenExecutionTrigger { void ExecuteTokenEffect(); }
}
