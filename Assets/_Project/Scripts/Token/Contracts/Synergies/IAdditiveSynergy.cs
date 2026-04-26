using System;
using System.Collections.Generic;

// MODIFIER — stacks its value additively with matching token types already active.
// CanApply: requires at least one instance of the same type already present (excluding self).
// Reverseable: subtracts the contributed value from the surviving token when removed.
namespace Core.Tokens
{
    public interface IAdditiveSynergy : ITokenSynergy, IReverseableSynergy
    {
        HashSet<Type> additiveSynergys { get; }
        AdditiveSynergyContext BuildContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) =>
            TokenContainerController.GetOtherToken(context.TokenContainerController, context.token) != null;

        public void ApplyAdditiveSynergy(AdditiveSynergyContext context);

        // Locates the surviving token of the same type and calls onReverse so the
        // implementor can subtract the value that was merged during ApplyAdditiveSynergy.
        void IReverseableSynergy.ReverseSynergy(TokenContainerController tokenContainer)
        {
            var surviving = TokenContainerController.GetOtherToken(tokenContainer, (TokenController)this);
            if (surviving == null) return;

            var ctx = BuildContext(new TokenAllocationContext(string.Empty, tokenContainer, (TokenController)this));
            ctx.onReverse?.Invoke(surviving);
        }
    }

    [Serializable]
    public struct AdditiveSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Called during reversal with the surviving token so the implementor subtracts its contribution.
        public Action<TokenController> onReverse;

        public AdditiveSynergyContext(TokenContainerController TokenContainerController, TokenController self, Action<TokenController> onReverse = null)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.onReverse = onReverse;
        }
    }
}