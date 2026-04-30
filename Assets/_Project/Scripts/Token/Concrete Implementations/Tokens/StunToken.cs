using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// DEBUFF — Atordoamento. Impede a unidade de agir no turno.
    /// Sinergias:
    ///   - Cancellation: cancela ComboToken presente (ritmo do combo quebrado pelo stun).
    ///   - Immunity: enquanto ativo, bloqueia TauntToken (unidade atordoada não provoca ninguém),
    ///     StealthToken (não consegue se esconder atordoada) e novo StunToken (sem stun duplo).
    /// Um único Stun por unidade; refresh na reaplicação.
    /// Allocation: condition-based — só aplica se não há Stun já ativo.
    /// </summary>
    public class StunToken : TokenController, ICancellationSynergy, IImmunitySynergy
    {
        private readonly Action<bool> setActingBlocked;

        public HashSet<Type> cancellationSynergys { get; } = new() { typeof(ComboToken) };
        public HashSet<Type> immunitySynergys     { get; } = new() { typeof(TauntToken), typeof(StealthToken), typeof(StunToken) };

        public StunToken(TokenContainerController container, Action<bool> setActingBlocked) : base(
            typeof(StunToken).Name,
            new RefreshDurationStackData(1),
            new IOnConditionMetTokenAllocation(
                () => !TokenContainerController.TokenTypeExistsInList<StunToken>(container)))
        {
            this.setActingBlocked = setActingBlocked;
        }

        public CancellationSynergyContext BuildCancellationContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this);

        public ImmunitySynergyContext BuildImmunityContext(TokenAllocationContext ctx) =>
            new(ctx.TokenContainerController, this);

        public override void ExecuteTokenEffect()
        {
            setActingBlocked?.Invoke(true);
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                "Stun — unit cannot act this turn");
            base.ExecuteTokenEffect();
        }
    }
}
