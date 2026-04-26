using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// A holy protection buff that purges damage-over-time effects on application.
    /// Overrides and removes <see cref="BurningStatusToken"/> and <see cref="PoisonToken"/>
    /// from the container while keeping itself active — a unilateral cleanse.
    /// Allocation style: event-based — triggered by a specific game event (e.g. cast, item use).
    /// </summary>
    public class DivineShieldToken : TokenController, IOverrideSynergy
    {
        public HashSet<Type> overrideSynergys { get; } = new HashSet<Type>
        {
            typeof(BurningStatusToken),
            typeof(PoisonToken)
        };

        public DivineShieldToken(TokenContainerController container) : base(
            typeof(DivineShieldToken).Name,
            new RefreshDurationStackData(3),
            new IOnEventTokenAllocation(null))
        { }

        public OverrideSynergyContext BuildContext(TokenAllocationContext context) =>
            new OverrideSynergyContext(context.TokenContainerController, this, () =>
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                    $"Divine Shield purged all DoT tokens from {context.TokenContainerController.name}"));

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
