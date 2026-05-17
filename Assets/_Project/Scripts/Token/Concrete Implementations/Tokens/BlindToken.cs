using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// DEBUFF — Cegueira. Impõe chance de erro em ataques da unidade afetada.
    /// Sinergias:
    ///   - Immunity: bloqueia ComboToken enquanto ativo — unidade cega não consegue executar combos.
    ///   - Cancellation: cancela StealthToken presente (unidade furtiva em pânico fica visível).
    /// Refresh na reaplicação.
    /// Allocation: on-hit.
    /// </summary>
    public class BlindToken : TokenController, IImmunitySynergy, ICancellationSynergy
    {
        private readonly Action<float> applyMissChance;
        public float missChance { get; } = 0.40f;

        public HashSet<Type> immunitySynergys     { get; } = new() { typeof(ComboToken) };
        public HashSet<Type> cancellationSynergys { get; } = new() { typeof(StealthToken) };

        public BlindToken(TokenContainerController container, Action<float> applyMissChance) : base(
            typeof(BlindToken).Name,
            new RefreshDurationStackData(2),
            new IOnConditionMetTokenAllocation(
                () => !TokenContainerController.TokenTypeExistsInList<BlindToken>(container)))
        {
            this.applyMissChance = applyMissChance;
        }

        public ImmunitySynergyContext BuildImmunityContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this);

        public CancellationSynergyContext BuildCancellationContext(TokenAllocationContext ctx) =>
            new(ctx.TokenContainerController, this);

        public override void ExecuteTokenEffect()
        {
            applyMissChance?.Invoke(missChance);
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                $"Blind — unit has {missChance * 100f:F0}% miss chance this turn");
            base.ExecuteTokenEffect();
        }
    }
}
