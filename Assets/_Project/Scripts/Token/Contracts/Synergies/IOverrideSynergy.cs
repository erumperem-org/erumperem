using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// POST-ALLOCATION — removes target tokens but keeps itself active.
// Unlike Cancellation, this token survives and takes precedence.
// CanApply: requires at least one override target present.
namespace Core.Tokens
{
    public interface IOverrideSynergy : ITokenSynergy
    {
        HashSet<Type> overrideSynergys { get; }
        OverrideSynergyContext BuildOverrideContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, overrideSynergys);

        public void ApplyOverrideSynergy(OverrideSynergyContext context)
        {
            TokenContainerController.RemoveFirstByTypes(context.TokenContainerController, overrideSynergys);
            context.onOverride?.Invoke();
        }
    }

    [Serializable]
    public struct OverrideSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Invoked after all overridden tokens have been removed.
        public Action onOverride;

        public OverrideSynergyContext(TokenContainerController TokenContainerController, TokenController self, Action onOverride)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.onOverride = onOverride;
        }
    }
}
