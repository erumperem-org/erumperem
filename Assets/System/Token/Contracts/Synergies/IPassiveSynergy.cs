using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// TICK BONUS — provides a recurring bonus while matching tokens are active.
// CanApply: always true — tick-driven, presence check happens inside Apply each tick.
namespace Core.Tokens
{
    public interface IPassiveSynergy : ITokenSynergy
    {
        HashSet<Type> passiveSynergys { get; }
        PassiveSynergyContext BuildContext(TokenAllocationContext context);
        bool ITokenSynergy.CanApply(TokenAllocationContext context) => true;

        public void ApplyPassiveSynergy(PassiveSynergyContext context)
        {
            int matchCount = TokenContainerController.CountByTypes(context.TokenContainerController, passiveSynergys);
            if (matchCount > 0)
                context.onPassiveTick?.Invoke(matchCount);
        }
    }

    [Serializable]
    public struct PassiveSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Called each tick with the number of currently matching tokens active.
        public Action<int> onPassiveTick;

        public PassiveSynergyContext(TokenContainerController TokenContainerController, TokenController self, Action<int> onPassiveTick)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.onPassiveTick = onPassiveTick;
        }
    }
}
