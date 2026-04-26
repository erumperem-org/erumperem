using System;
using System.Collections.Generic;

// MODIFIER — increases this token's effectiveness based on how many matching tokens are active.
// Binary presence check: set amplifierPerStack = 0 and matchCount >= 1.
// CanApply: requires at least one amplifier token present.
// Reverseable: restores the amplified value to its base when this token is removed.
namespace Core.Tokens
{
    public interface IAmplificationSynergy : ITokenSynergy, IReverseableSynergy
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

        // Calls onAmplify with 0 so the implementor resets the stored value to its base.
        // Convention: the implementor must treat 0 as "no amplification active".
        void IReverseableSynergy.ReverseSynergy(TokenContainerController tokenContainer)
        {
            var ctx = BuildContext(new TokenAllocationContext(string.Empty, tokenContainer, (TokenController)this));
            ctx.onAmplify?.Invoke(0f);
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