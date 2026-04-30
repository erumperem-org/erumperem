using System;
using System.Collections.Generic;

// TICK BONUS — provides a recurring bonus while matching tokens are active.
// CanApply: always true — tick-driven, presence check happens inside Apply each tick.
// Reverseable: calls onReverseAccumulated so the implementor subtracts any bonus
// that was built up across ticks before the token was removed.
namespace Core.Tokens
{
    public interface IPassiveSynergy : ITokenSynergy, IReverseableSynergy
    {
        HashSet<Type> passiveSynergys { get; }
        PassiveSynergyContext BuildPassiveContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => true;

        public void ApplyPassiveSynergy(PassiveSynergyContext context)
        {
            int matchCount = TokenContainerController.CountByTypes(context.TokenContainerController, passiveSynergys);
            if (matchCount > 0)
                context.onPassiveTick?.Invoke(matchCount);
        }

        // Calls onReverseAccumulated so the implementor can subtract whatever
        // bonus accumulated across ticks before this token was removed.
        // If the passive had no lasting side-effects, onReverseAccumulated may be left null.
        void IReverseableSynergy.ReverseSynergy(TokenContainerController tokenContainer)
        {
            var ctx = BuildPassiveContext(new TokenAllocationContext(string.Empty, tokenContainer, (TokenController)this));
            ctx.onReverseAccumulated?.Invoke();
        }
    }

    [Serializable]
    public struct PassiveSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Called each tick with the number of currently matching tokens active.
        public Action<int> onPassiveTick;
        // Called once on removal; implementor undoes any accumulated bonus.
        public Action onReverseAccumulated;

        public PassiveSynergyContext(TokenContainerController TokenContainerController, TokenController self, Action<int> onPassiveTick, Action onReverseAccumulated = null)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.onPassiveTick = onPassiveTick;
            this.onReverseAccumulated = onReverseAccumulated;
        }
    }
}