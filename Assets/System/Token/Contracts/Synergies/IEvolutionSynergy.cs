using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// CONSUMER — upgrades this token into a stronger form once a threshold of matching tokens is reached.
// CanApply: requires matchCount >= evolutionThreshold.
namespace Core.Tokens
{
    public interface IEvolutionSynergy : ITokenSynergy
    {
        HashSet<Type> evolutionSynergys { get; }
        int evolutionThreshold { get; }
        EvolutionSynergyContext BuildContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.CountByTypes(context.TokenContainerController, evolutionSynergys) >= evolutionThreshold;

        public async Task ApplyEvolutionSynergy(EvolutionSynergyContext context)
        {
            int removed = 0;
            var typeSet = new HashSet<Type>(evolutionSynergys);
            var list = context.TokenContainerController.model.tokens;

            for (int i = list.Count - 1; i >= 0 && removed < evolutionThreshold; i--)
            {
                if (typeSet.Contains(list[i].GetType()))
                {
                    TokenContainerController.RemoveTokenFromContainer(context.TokenContainerController, list[i]);
                    removed++;
                }
            }
            await TokenContainerController.AddTokenToContainer(new TokenAllocationContext(context.ownerName, context.TokenContainerController, context.evolvedToken));
        }
    }

    [Serializable]
    public struct EvolutionSynergyContext
    {
        public string ownerName;
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // The upgraded token that replaces all participants.
        public TokenController evolvedToken;

        public EvolutionSynergyContext(string ownerName, TokenContainerController TokenContainerController, TokenController self, TokenController evolvedToken)
        {
            this.ownerName = ownerName;
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.evolvedToken = evolvedToken;
        }
    }
}
