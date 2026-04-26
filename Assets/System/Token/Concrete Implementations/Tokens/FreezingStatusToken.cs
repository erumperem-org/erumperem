using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A status token that represents the frozen condition applied to a target.
    /// Blocks <see cref="BurningStatusToken"/> from being allocated while active — 
    /// fire and ice are mutually exclusive states.
    /// Refreshes its duration on reapplication rather than stacking.
    /// Allocation style: condition-based — only applies if no Freezing is already present.
    /// </summary>
    public class FreezingStatusToken : TokenController, IImmunitySynergy
    {
        public HashSet<Type> immunitySynergys { get; } = new HashSet<Type> { typeof(BurningStatusToken) };

        public FreezingStatusToken(TokenContainerController container, int maxDuration) : base(
            typeof(FreezingStatusToken).Name,
            new RefreshDurationStackData(maxDuration),
            new IOnConditionMetTokenAllocation(() => !TokenContainerController.TokenTypeExistsInList<FreezingStatusToken>(container)))
        { }

        public ImmunitySynergyContext BuildContext(TokenAllocationContext context) =>
            new ImmunitySynergyContext(context.TokenContainerController, this);

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
