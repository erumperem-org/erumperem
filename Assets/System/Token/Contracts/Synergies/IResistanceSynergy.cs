using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// MODIFIER — reduces the effect, duration, or intensity of a target token.
// Convention: resistanceFactor is a value between 0 (full block) and 1 (no reduction).
// CanApply: requires at least one resisted token type present.
namespace Core.Tokens
{
    public interface IResistanceSynergy : ITokenSynergy
    {
        HashSet<Type> resistanceSynergys { get; }
        ResistanceSynergyContext BuildContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, resistanceSynergys);

        public void ApplyResistanceSynergy(ResistanceSynergyContext context);
    }

    [Serializable]
    public struct ResistanceSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // 0 = fully blocked, 1 = unaffected. Set by the implementing token.
        public float resistanceFactor;

        public ResistanceSynergyContext(TokenContainerController TokenContainerController, TokenController self, float resistanceFactor)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.resistanceFactor = resistanceFactor;
        }
    }
}
