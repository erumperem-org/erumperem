using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// A sustain passive that heals per active regeneration stack each turn.
    /// Each turn, for every active <see cref="RegenToken"/> in the container,
    /// restores <see cref="healPerRegenStack"/> health to the holder.
    /// Requires an external tick source (turn manager or Update loop) to call
    /// ApplyPassiveSynergy each turn — does nothing if called only once.
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class RegenerationAuraToken : TokenController, IPassiveSynergy
    {
        public HashSet<Type> passiveSynergys { get; } = new HashSet<Type> { typeof(RegenToken) };
        public int healPerRegenStack = 5;

        public RegenerationAuraToken() : base(
            typeof(RegenerationAuraToken).Name,
            new RefreshDurationStackData(4),
            new IOnHitTokenAllocation())
        { }

        public PassiveSynergyContext BuildContext(TokenAllocationContext context) =>
            new PassiveSynergyContext(context.TokenContainerController, this,
                matchCount => LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                    $"Regeneration Aura healing {matchCount * healPerRegenStack} per tick"));

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
