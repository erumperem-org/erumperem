using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// POST-ALLOCATION REACTION — after being added, this token removes target
// tokens from the container and then removes itself (mutual cancellation).
// CanApply: requires at least one cancellation target present.
namespace Core.Tokens
{
    public interface ICancellationSynergy : ITokenSynergy
    {
        HashSet<Type> cancellationSynergys { get; }
        CancellationSynergyContext BuildContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, cancellationSynergys);

        public void ApplyCancellationSynergy(CancellationSynergyContext context)
        {
            TokenContainerController.RemoveFirstByTypes(context.TokenContainerController, cancellationSynergys);
            TokenContainerController.RemoveTokenFromContainer(context.TokenContainerController, context.self);
        }
    }

    [Serializable]
    public struct CancellationSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;

        public CancellationSynergyContext(TokenContainerController TokenContainerController, TokenController self)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
        }
    }
}
