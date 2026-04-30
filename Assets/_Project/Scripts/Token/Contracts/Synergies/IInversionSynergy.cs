using System;
using System.Collections.Generic;

// MODIFIER — reverses this token's effect into its opposite when matching types are present.
// CanApply: requires at least one inversion trigger token present.
// Reverseable: calls onRestore to flip the token's behaviour back to its original form.
namespace Core.Tokens
{
    public interface IInversionSynergy : ITokenSynergy, IReverseableSynergy
    {
        HashSet<Type> inversionSynergys { get; }
        InversionSynergyContext BuildInversionContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, inversionSynergys);

        public void ApplyInversionSynergy(InversionSynergyContext context)
        {
            context.onInvert?.Invoke();
        }

        // Calls onRestore so the implementor flips the behaviour back to its original form.
        void IReverseableSynergy.ReverseSynergy(TokenContainerController tokenContainer)
        {
            var ctx = BuildInversionContext(new TokenAllocationContext(string.Empty, tokenContainer, (TokenController)this));
            ctx.onRestore?.Invoke();
        }
    }

    [Serializable]
    public struct InversionSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Implementor flips the token's effect to its opposite here.
        public Action onInvert;
        // Implementor flips the token's effect back to its original form here.
        public Action onRestore;

        public InversionSynergyContext(TokenContainerController TokenContainerController, TokenController self, Action onInvert, Action onRestore)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.onInvert = onInvert;
            this.onRestore = onRestore;
        }
    }
}