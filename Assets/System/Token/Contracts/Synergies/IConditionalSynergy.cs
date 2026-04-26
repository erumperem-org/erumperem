using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// CONDITIONAL TRIGGER — fires an effect when an arbitrary condition is true.
// CanApply: always true — the Func<bool> condition is the gate, evaluated inside Apply.
namespace Core.Tokens
{
    public interface IConditionalSynergy : ITokenSynergy
    {
        ConditionalSynergyContext BuildContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => true;

        public void ApplyConditionalSynergy(ConditionalSynergyContext context)
        {
            if (context.condition?.Invoke() == true)
                context.onConditionMet?.Invoke();
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
        // Human-readable description of what this condition represents.
        public string conditionDescription;

        public ConditionalSynergyContext(TokenContainerController TokenContainerController, TokenController self, Func<bool> condition, Action onConditionMet, string conditionDescription = "")
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.condition = condition;
            this.onConditionMet = onConditionMet;
            this.conditionDescription = conditionDescription;
        }
    }
}
