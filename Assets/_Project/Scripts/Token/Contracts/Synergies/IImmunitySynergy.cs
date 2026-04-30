using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// PRE-ALLOCATION GUARD — blocks the incoming token from being added if a
// matching immunity type is already active in the container.
// Evaluated before the stacking strategy runs.
namespace Core.Tokens
{
    public interface IImmunitySynergy : ITokenSynergy
    {
        HashSet<Type> immunitySynergys { get; }
        ImmunitySynergyContext BuildImmunityContext(TokenAllocationContext context);

        // Returns true if the token should be blocked.
        public bool CheckImmunity(ImmunitySynergyContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, immunitySynergys);
    }

    [Serializable]
    public struct ImmunitySynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;

        public ImmunitySynergyContext(TokenContainerController TokenContainerController, TokenController self)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
        }
    }
}
