using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// CONSUMER — immediately consumes this token and one matching token, replacing both with a result.
// CanApply: requires exactly the target type present.
namespace Core.Tokens
{
    public interface ITransformationSynergy : ITokenSynergy
    {
        HashSet<Type> transformationSynergys { get; }
        TransformationSynergyContext BuildContext(TokenAllocationContext context);
        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, transformationSynergys);
        public async Task ApplyTransformationSynergy(TransformationSynergyContext context)
        {
            TokenContainerController.RemoveFirstByTypes(context.TokenContainerController, transformationSynergys);
            TokenContainerController.RemoveTokenFromContainer(context.TokenContainerController, context.self);
            await TokenContainerController.AddTokenToContainer(new TokenAllocationContext(context.ownerName, context.TokenContainerController, context.resultToken));
        }
    }

    [Serializable]
    public struct TransformationSynergyContext
    {
        public string ownerName;
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // The token that replaces both participants.
        public TokenController resultToken;

        public TransformationSynergyContext(string ownername, TokenContainerController TokenContainerController, TokenController self, TokenController resultToken)
        {
            this.ownerName = ownername;
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.resultToken = resultToken;
        }
    }
}
