using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// A retaliation passive that deals reflect damage scaled by active bleed stacks.
    /// Each turn, for every active <see cref="BleedToken"/> in the container,
    /// reflects <see cref="damagePerBleedStack"/> damage back to the attacker.
    /// Requires an external tick source (turn manager or Update loop) to call
    /// ApplyPassiveSynergy each turn — does nothing if called only once.
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class ThornToken : TokenController, IPassiveSynergy
    {
        public HashSet<Type> passiveSynergys { get; } = new HashSet<Type> { typeof(BleedToken) };
        public int damagePerBleedStack = 3;

        public ThornToken() : base(
            typeof(ThornToken).Name,
            new RefreshDurationStackData(4),
            new IOnHitTokenAllocation())
        { }

        public PassiveSynergyContext BuildPassiveContext(TokenAllocationContext context) =>
            new PassiveSynergyContext(context.TokenContainerController, this,
                matchCount => LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                    $"Thorn dealing {matchCount * damagePerBleedStack} reflect damage"));

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
