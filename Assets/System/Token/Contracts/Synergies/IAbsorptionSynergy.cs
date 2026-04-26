using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// POST-ALLOCATION — removes target tokens and converts them into a benefit.
// Unlike Cancellation (mutual) and Override (neutral removal), Absorption
// is unilateral and profitable: the absorber gains something from each token consumed.
// CanApply: requires at least one absorbable target present.
namespace Core.Tokens
{
    public interface IAbsorptionSynergy : ITokenSynergy
    {
        HashSet<Type> absorptionSynergys { get; }
        AbsorptionSynergyContext BuildContext(TokenAllocationContext context);
        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, absorptionSynergys);
        public void ApplyAbsorptionSynergy(AbsorptionSynergyContext context) => TokenContainerController.RemoveByTypes(context.TokenContainerController, absorptionSynergys, token => context.onAbsorb?.Invoke(token));
    }

    [Serializable]
    public struct AbsorptionSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Called for each absorbed token; implementor decides the benefit gained.
        public Action<TokenController> onAbsorb;

        public AbsorptionSynergyContext(TokenContainerController TokenContainerController, TokenController self, Action<TokenController> onAbsorb)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.onAbsorb = onAbsorb;
        }
    }
}
