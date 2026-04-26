using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// PROPAGATION — copies this token to other containers that do not yet hold it.
// CanApply: always true — eligibility is evaluated per-container inside ApplySpreadSynergy.
namespace Core.Tokens
{
    public interface ISpreadSynergy : ITokenSynergy
    {
        SpreadSynergyContext BuildContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => true;

        public void ApplySpreadSynergy(SpreadSynergyContext context)
        {
            foreach (var target in context.spreadTargets)
            {
                bool alreadyPresent = TokenContainerController.HasSameTokenType(target, context.self);
                if (!alreadyPresent)
                    context.onSpread?.Invoke(target);
            }
        }
    }

    [Serializable]
    public struct SpreadSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // All containers eligible to receive the spread.
        public List<TokenContainerController> spreadTargets;
        // Called per eligible target; implementor allocates the token there.
        public Action<TokenContainerController> onSpread;

        public SpreadSynergyContext(TokenContainerController TokenContainerController, TokenController self, List<TokenContainerController> spreadTargets, Action<TokenContainerController> onSpread)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.spreadTargets = spreadTargets;
            this.onSpread = onSpread;
        }
    }
}
