using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// MODIFIER — increases this token's effectiveness based on how many matching tokens are active.
// Binary presence check: set amplifierPerStack = 0 and matchCount >= 1.
// CanApply: requires at least one amplifier token present.
namespace Core.Tokens
{
    public interface IAmplificationSynergy : ITokenSynergy
    {
        HashSet<Type> amplificationSynergys { get; }
        AmplificationSynergyContext BuildContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, amplificationSynergys);

        public void ApplyAmplificationSynergy(AmplificationSynergyContext context)
        {
            int matchCount = TokenContainerController.CountByTypes(context.TokenContainerController, amplificationSynergys);

            if (matchCount > 0)
                context.onAmplify?.Invoke(matchCount * context.amplifierPerStack);
        }
    }

    [Serializable]
    public struct AmplificationSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Multiplier added per matching token found. Set 0 for binary (presence-only) checks.
        public float amplifierPerStack;
        // Called with the total amplification value.
        public Action<float> onAmplify;

        public AmplificationSynergyContext(TokenContainerController TokenContainerController, TokenController self, float amplifierPerStack, Action<float> onAmplify)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.amplifierPerStack = amplifierPerStack;
            this.onAmplify = onAmplify;
        }
    }
}
