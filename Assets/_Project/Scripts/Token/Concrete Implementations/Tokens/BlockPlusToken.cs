using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// BUFF — Mitigação maior de dano. Versão aprimorada do Block.
    /// Amplifica BlockTokens já presentes no mesmo container.
    /// Sinergias:
    ///   - Cancellation: cancela BleedToken presente (armadura superior sela o ferimento).
    ///   - Immunity: enquanto ativo, impede novos BleedTokens de serem alocados.
    /// Allocation: on-event.
    /// </summary>
    public class BlockPlusToken : TokenController, ICancellationSynergy, IImmunitySynergy
    {
        public float blockedFraction { get; } = 0.30f;

        public HashSet<Type> cancellationSynergys { get; } = new() { typeof(BleedToken) };
        public HashSet<Type> immunitySynergys     { get; } = new() { typeof(BleedToken) };

        public BlockPlusToken() : base(
            typeof(BlockPlusToken).Name,
            new RefreshDurationStackData(3),
            new IOnEventTokenAllocation(null))
        { }

        public CancellationSynergyContext BuildCancellationContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this);

        ImmunitySynergyContext IImmunitySynergy.BuildImmunityContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this);

        public override void ExecuteTokenEffect()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                $"Block+ active — mitigating {blockedFraction * 100f:F0}% of incoming damage");
            base.ExecuteTokenEffect();
        }
    }
}
