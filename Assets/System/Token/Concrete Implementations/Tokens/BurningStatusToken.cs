using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A status token that represents the burning condition applied to a target.
    /// Blocks <see cref="FreezingStatusToken"/> from being allocated while active —
    /// fire and ice are mutually exclusive states.
    /// Refreshes its duration on reapplication rather than stacking.
    /// Allocation style: condition-based — only applies if no Burning is already present.
    /// </summary>
    public class BurningStatusToken : TokenController, IImmunitySynergy
    {
        public HashSet<Type> immunitySynergys { get; } = new HashSet<Type> { typeof(FreezingStatusToken) };

        public BurningStatusToken(TokenContainerController container, int maxDuration) : base(
            typeof(BurningStatusToken).Name,
            new RefreshDurationStackData(maxDuration),
            new IOnConditionMetTokenAllocation(() => !TokenContainerController.TokenTypeExistsInList<BurningStatusToken>(container)))
        { }

        public ImmunitySynergyContext BuildContext(TokenAllocationContext context) =>
            new ImmunitySynergyContext(context.TokenContainerController, this);

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
