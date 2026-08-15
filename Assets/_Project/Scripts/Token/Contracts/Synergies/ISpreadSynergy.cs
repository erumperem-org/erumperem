using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities;
using System.Threading.Tasks;

// PROPAGATION — copies this token to other containers that do not yet hold it.
// CanApply: always true — eligibility is evaluated per-container inside ApplySpreadSynergy.
namespace Core.Tokens
{
    public interface ISpreadSynergy : ITokenSynergy
    {
        SpreadSynergyContext BuildSpreadContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => true;

        public async Task ApplySpreadSynergy(SpreadSynergyContext context)
        {
            foreach (var target in context.spreadTargets)
            {
                bool alreadyPresent = TokenContainerController.HasSameTokenType(target, context.self);
                if (!alreadyPresent && context.onSpread != null)
                    await context.onSpread(target);
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
        public Func<TokenContainerController, Task> onSpread;

        public SpreadSynergyContext(
            TokenContainerController TokenContainerController,
            TokenController self,
            List<TokenContainerController> spreadTargets,
            Func<TokenContainerController, Task> onSpread)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.spreadTargets = spreadTargets;
            this.onSpread = onSpread;
        }
    }
}
