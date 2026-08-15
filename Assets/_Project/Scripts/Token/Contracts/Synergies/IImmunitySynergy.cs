using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities;
using System.Threading.Tasks;

// PRE-ALLOCATION GUARD — an active token blocks incoming token types declared
// in its immunity set. Evaluated before the stacking strategy runs.
namespace Core.Tokens
{
    public interface IImmunitySynergy : ITokenSynergy
    {
        HashSet<Type> immunitySynergys { get; }
        ImmunitySynergyContext BuildImmunityContext(TokenAllocationContext context);

        // The controller builds this context from the active immunity source and
        // supplies the incoming token before evaluating the guard.
        public bool CheckImmunity(ImmunitySynergyContext context) =>
            context.incomingToken != null
            && immunitySynergys.Contains(context.incomingToken.GetType());
    }

    [Serializable]
    public struct ImmunitySynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        public TokenController incomingToken;

        public ImmunitySynergyContext(TokenContainerController TokenContainerController, TokenController self)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.incomingToken = null;
        }
    }
}
