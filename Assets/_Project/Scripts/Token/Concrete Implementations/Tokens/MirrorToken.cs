using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A reflective buff that inverts the effect of bleeding wounds into healing.
    /// When allocated alongside <see cref="BleedToken"/>, sets <see cref="isInverted"/>
    /// to true — signaling that BleedToken's damage should be treated as healing
    /// during ExecuteTokenEffect.
    /// Allocation style: event-based — triggered by a specific game event.
    /// </summary>
    public class MirrorToken : TokenController, IInversionSynergy
    {
        public HashSet<Type> inversionSynergys { get; } = new HashSet<Type> { typeof(BleedToken) };
        public bool isInverted = false;

        public MirrorToken() : base(
            typeof(MirrorToken).Name,
            new RefreshDurationStackData(3),
            new IOnEventTokenAllocation(null))
        { }

        public InversionSynergyContext BuildContext(TokenAllocationContext context) =>
            new InversionSynergyContext(context.TokenContainerController, this,
                () => isInverted = true,
                () => isInverted = false);

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
