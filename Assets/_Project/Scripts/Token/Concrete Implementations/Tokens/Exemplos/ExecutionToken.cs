using System;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// A finishing blow token that triggers a lethal effect when the target is near death.
    /// The condition is evaluated on every synergy tick — when <see cref="lowHealthCheck"/>
    /// returns true (target HP below 20%), the execution fires immediately.
    /// The condition delegate is provided externally by the combat system.
    /// Allocation style: condition-based — only applies when the low-health condition is met.
    /// </summary>
    public class ExecutionToken : TokenController, IConditionalSynergy
    {
        private readonly Func<bool> lowHealthCheck;

        public ExecutionToken(TokenContainerController container, Func<bool> isLowHealth) : base(
            typeof(ExecutionToken).Name,
            new IndependentStackData(),
            new IOnConditionMetTokenAllocation(isLowHealth))
        {
            lowHealthCheck = isLowHealth;
        }

        public ConditionalSynergyContext BuildConditionalContext(TokenAllocationContext context) =>
            new ConditionalSynergyContext(context.TokenContainerController, this,
                lowHealthCheck,
                () => LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                    $"Execution triggered on {context.TokenContainerController.name}"),
                conditionDescription: "Target HP below 20%");

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
