using System;
using System.Collections.Generic;

// CONDITIONAL TRIGGER — fires an effect when an arbitrary condition is true.
// CanApply: always true — the Func<bool> condition is the gate, evaluated inside Apply.
// Reverseable: calls onRevert if the condition had already fired and left external state dirty.
namespace Core.Tokens
{
    public interface IConditionalSynergy : ITokenSynergy, IReverseableSynergy
    {
        ConditionalSynergyContext BuildConditionalContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => true;

        public void ApplyConditionalSynergy(ConditionalSynergyContext context)
        {
            if (context.condition?.Invoke() == true)
            {
                context.onConditionMet?.Invoke();
                context.hasFired = true;
            }
        }

        // Calls onRevert only if the condition had previously fired (hasFired == true),
        // so tokens whose condition never triggered do not attempt a spurious revert.
        void IReverseableSynergy.ReverseSynergy(TokenContainerController tokenContainer)
        {
            var ctx = BuildConditionalContext(new TokenAllocationContext(string.Empty, tokenContainer, (TokenController)this));
            if (ctx.hasFired)
                ctx.onRevert?.Invoke();
        }
    }

    [Serializable]
    public struct ConditionalSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Any condition: state check, timer, event flag, etc.
        public Func<bool> condition;
        public Action onConditionMet;
        // Tracks whether onConditionMet has already fired at least once.
        public bool hasFired;
        // Called on removal if hasFired is true; implementor undoes whatever onConditionMet did.
        public Action onRevert;
        // Human-readable description of what this condition represents.
        public string conditionDescription;

        public ConditionalSynergyContext(TokenContainerController TokenContainerController, TokenController self, Func<bool> condition, Action onConditionMet, Action onRevert = null, string conditionDescription = "")
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.condition = condition;
            this.onConditionMet = onConditionMet;
            this.hasFired = false;
            this.onRevert = onRevert;
            this.conditionDescription = conditionDescription;
        }
    }
}