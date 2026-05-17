using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// BUFF — Mitigação parcial de dano. Cada stack reduz o dano recebido em blockedFraction.
    /// Stacks se somam aditivamente com outros BlockTokens já ativos.
    /// Sinergias:
    ///   - Additive: fusão com outro BlockToken presente — soma os valores de mitigação.
    ///   - Amplification: BlockPlusToken presente amplifica a fração bloqueada.
    /// Allocation: on-event (ganho de defesa).
    /// </summary>
    public class BlockToken : TokenController, IAdditiveSynergy, IAmplificationSynergy
    {
        public float blockedFraction { get; private set; }
        private float currentAmplifier = 1f;

        public HashSet<Type> additiveSynergys      { get; } = new() { typeof(BlockToken) };
        public HashSet<Type> amplificationSynergys { get; } = new() { typeof(BlockPlusToken) };

        public BlockToken(float blockedFraction = 0.15f) : base(
            typeof(BlockToken).Name,
            new LinearStackData(0f),
            new IOnEventTokenAllocation(null))
        {
            this.blockedFraction = blockedFraction;
        }

        public AdditiveSynergyContext BuildAdditiveContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this,
                onReverse: surviving =>
                {
                    if (surviving is BlockToken other)
                        other.blockedFraction -= blockedFraction;
                });

        public void ApplyAdditiveSynergy(AdditiveSynergyContext context)
        {
            var other = TokenContainerController.GetOtherToken(context.TokenContainerController, this);
            if (other is BlockToken existing)
                existing.blockedFraction += blockedFraction;
        }

        public AmplificationSynergyContext BuildAmplificationContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this,
                amplifierPerStack: 0.05f,
                onAmplify: v => currentAmplifier = (v == 0f) ? 1f : 1f + v);

        public void ReverseSynergy(TokenContainerController tokenContainer)
        {
            var surviving = TokenContainerController.GetOtherToken(tokenContainer, this);
            if (surviving == null) return;

            var ctx = BuildAdditiveContext(new TokenAllocationContext(string.Empty, tokenContainer, this));
            ctx.onReverse?.Invoke(surviving);
        }

        public bool CanApply(TokenAllocationContext context) =>
            TokenContainerController.GetOtherToken(context.TokenContainerController, context.token) != null;

        public float EffectiveFraction => blockedFraction * currentAmplifier;

        public override void ExecuteTokenEffect()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                $"Block active — mitigating {EffectiveFraction * 100f:F0}% of incoming damage");
            base.ExecuteTokenEffect();
        }
    }
}