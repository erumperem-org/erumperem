using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// MODIFIER — stacks its value additively with matching token types already active.
// CanApply: requires at least one instance of the same type already present (excluding self).
namespace Core.Tokens
{
    public interface IAdditiveSynergy : ITokenSynergy
    {
        HashSet<Type> additiveSynergys { get; }
        AdditiveSynergyContext BuildContext(TokenAllocationContext context);
        bool ITokenSynergy.CanApply(TokenAllocationContext context) =>
            TokenContainerController.GetOtherToken(context.TokenContainerController, context.token) != null;
        public void ApplyAdditiveSynergy(AdditiveSynergyContext context);
    }

    [Serializable]
    public struct AdditiveSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;

        public AdditiveSynergyContext(TokenContainerController TokenContainerController, TokenController self)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
        }
    }
}
