using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// MODIFIER — reverses this token's effect into its opposite when matching types are present.
// CanApply: requires at least one inversion trigger token present.
namespace Core.Tokens
{
    public interface IInversionSynergy : ITokenSynergy
    {
        HashSet<Type> inversionSynergys { get; }
        InversionSynergyContext BuildContext(TokenAllocationContext context);
        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, inversionSynergys);
        public void ApplyInversionSynergy(InversionSynergyContext context)
        {
            context.onInvert?.Invoke();
        }
    }

    [Serializable]
    public struct InversionSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Implementor flips the token's effect to its opposite here.
        public Action onInvert;

        public InversionSynergyContext(TokenContainerController TokenContainerController, TokenController self, Action onInvert)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.onInvert = onInvert;
        }
    }
}
