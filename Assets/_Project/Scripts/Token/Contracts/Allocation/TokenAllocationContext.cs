using System;
using Services.DebugUtilities.Console;

// ALLOCATION
// Defines when the token is initially applied.

namespace Core.Tokens
{
    // Defines the context passed through the allocation pipeline.
    [Serializable]
    public struct TokenAllocationContext
    {
        public string ownerName;
        public TokenContainerController TokenContainerController;
        public TokenController token;

        public TokenAllocationContext(string ownerName, TokenContainerController target, TokenController token)
        {
            this.ownerName = ownerName;
            this.TokenContainerController = target;
            this.token = token;
        }
    }
}
