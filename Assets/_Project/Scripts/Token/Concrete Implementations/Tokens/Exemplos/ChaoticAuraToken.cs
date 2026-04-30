using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A chaos debuff that corrupts healing into harm.
    /// When allocated alongside <see cref="RegenToken"/>, sets <see cref="isInverted"/>
    /// to true — signaling that RegenToken's healing should be treated as damage
    /// during ExecuteTokenEffect.
    /// Allocation style: event-based — triggered by a specific game event.
    /// </summary>
    public class ChaoticAuraToken : TokenController, IInversionSynergy
    {
        public HashSet<Type> inversionSynergys { get; } = new HashSet<Type> { typeof(RegenToken) };
        public bool isInverted = false;

        public ChaoticAuraToken() : base(
            typeof(ChaoticAuraToken).Name,
            new RefreshDurationStackData(2),
            new IOnEventTokenAllocation(null))
        { }

        public InversionSynergyContext BuildInversionContext(TokenAllocationContext context) =>
            new InversionSynergyContext(context.TokenContainerController, this,
                () => isInverted = true,
                () => isInverted = false);

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
