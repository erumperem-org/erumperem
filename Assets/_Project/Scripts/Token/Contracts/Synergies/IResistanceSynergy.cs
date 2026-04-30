using System;
using System.Collections.Generic;

// MODIFIER — reduces the effect, duration, or intensity of a target token.
// Convention: resistanceFactor is a value between 0 (full block) and 1 (no reduction).
// CanApply: requires at least one resisted token type present.
// Reverseable: restores resistanceFactor to 1 (no reduction) on the resisted tokens when removed.
namespace Core.Tokens
{
    public interface IResistanceSynergy : ITokenSynergy, IReverseableSynergy
    {
        HashSet<Type> resistanceSynergys { get; }
        ResistanceSynergyContext BuildResistanceContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, resistanceSynergys);

        public void ApplyResistanceSynergy(ResistanceSynergyContext context);

        // Rebuilds the context with resistanceFactor = 1 (unaffected) and re-applies,
        // so the resisted tokens return to full effect.
        void IReverseableSynergy.ReverseSynergy(TokenContainerController tokenContainer)
        {
            var ctx = BuildResistanceContext(new TokenAllocationContext(string.Empty, tokenContainer, (TokenController)this));
            var restored = new ResistanceSynergyContext(ctx.TokenContainerController, ctx.self, 1f);
            ApplyResistanceSynergy(restored);
        }
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