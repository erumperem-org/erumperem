using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;

namespace Core.Tokens
{
    /// <summary>
    /// A dark debuff that suppresses protective effects on application.
    /// Overrides and removes <see cref="DivineShieldToken"/> and <see cref="RegenToken"/>
    /// from the container while keeping itself active — a unilateral suppression.
    /// Allocation style: event-based — triggered by a specific game event (e.g. cast, curse ability).
    /// </summary>
    public class CurseToken : TokenController, IOverrideSynergy
    {
        public HashSet<Type> overrideSynergys { get; } = new HashSet<Type>
        {
            typeof(DivineShieldToken),
            typeof(RegenToken)
        };

        public CurseToken(TokenContainerController container) : base(
            typeof(CurseToken).Name,
            new RefreshDurationStackData(4),
            new IOnEventTokenAllocation(null))
        { }

        public OverrideSynergyContext BuildOverrideContext(TokenAllocationContext context) =>
            new OverrideSynergyContext(context.TokenContainerController, this, () =>
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"Curse suppressed all buff tokens on {context.TokenContainerController.name}", LogCategory.Combat));

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
