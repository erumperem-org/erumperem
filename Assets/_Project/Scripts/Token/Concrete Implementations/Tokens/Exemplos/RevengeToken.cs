using System;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// A retaliation token that triggers a counter-attack when the holder is struck.
    /// The condition is evaluated on every synergy tick — when <see cref="tookDamageThisTurn"/>
    /// returns true, the revenge effect fires immediately.
    /// The condition delegate is provided externally by the combat system.
    /// Allocation style: event-based — registered when a specific event occurs (e.g. equip, ability).
    /// </summary>
    public class RevengeToken : TokenController, IConditionalSynergy
    {
        private readonly Func<bool> tookDamageThisTurn;

        public RevengeToken(TokenContainerController container, Func<bool> tookDamage) : base(
            typeof(RevengeToken).Name,
            new RefreshDurationStackData(3),
            new IOnEventTokenAllocation(null))
        {
            tookDamageThisTurn = tookDamage;
        }

        public ConditionalSynergyContext BuildConditionalContext(TokenAllocationContext context) =>
            new ConditionalSynergyContext(context.TokenContainerController, this,
                tookDamageThisTurn,
                () => LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                    $"Revenge counter-attack triggered from {context.TokenContainerController.name}"),
                conditionDescription: "Holder took damage this turn");

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
