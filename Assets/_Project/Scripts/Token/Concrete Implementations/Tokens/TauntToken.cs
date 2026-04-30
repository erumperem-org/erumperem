using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// DEBUFF — Provocação. Força os inimigos a atacarem esta unidade.
    /// Sinergias:
    ///   - Cancellation: cancela StealthToken (não se pode ignorar uma unidade que provoca).
    ///   - Immunity: enquanto Taunt ativo, impede StealthToken de ser alocado no mesmo alvo.
    /// Um único Taunt por unidade; refresh na reaplicação.
    /// Allocation: on-event (habilidade de provocação).
    /// </summary>
    public class TauntToken : TokenController, ICancellationSynergy, IImmunitySynergy
    {
        private readonly Action onTauntApplied;

        public HashSet<Type> cancellationSynergys { get; } = new() { typeof(StealthToken) };
        public HashSet<Type> immunitySynergys     { get; } = new() { typeof(StealthToken) };

        public TauntToken(TokenContainerController container, Action onTauntApplied) : base(
            typeof(TauntToken).Name,
            new RefreshDurationStackData(2),
            new IOnConditionMetTokenAllocation(
                () => !TokenContainerController.TokenTypeExistsInList<TauntToken>(container)))
        {
            this.onTauntApplied = onTauntApplied;
        }

        public CancellationSynergyContext BuildCancellationContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this);

        public ImmunitySynergyContext BuildImmunityContext(TokenAllocationContext ctx) =>
            new(ctx.TokenContainerController, this);

        public override void ExecuteTokenEffect()
        {
            onTauntApplied?.Invoke();
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                $"Taunt — all enemies must target {data.tokenDisplayName} owner");
            base.ExecuteTokenEffect();
        }
    }
}
