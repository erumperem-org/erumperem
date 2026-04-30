using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A frost explosion that intensifies in the presence of freezing effects.
    /// For every active <see cref="FreezingStatusToken"/> in the container, the slow
    /// multiplier increases by 0.25 — deep freeze amplifies the nova's chill.
    /// Only applies when FreezingStatusToken is already present.
    /// The computed multiplier is stored in <see cref="slowMultiplier"/> for
    /// use during ExecuteTokenEffect.
    /// Allocation style: condition-based — requires FreezingStatusToken to be active.
    /// </summary>
    public class FrostNovaToken : TokenController, IAmplificationSynergy
    {
        public HashSet<Type> amplificationSynergys { get; } = new HashSet<Type> { typeof(FreezingStatusToken) };
        public float slowMultiplier = 1f;

        public FrostNovaToken(TokenContainerController container) : base(
            typeof(FrostNovaToken).Name,
            new RefreshDurationStackData(2),
            new IOnConditionMetTokenAllocation(() => TokenContainerController.TokenTypeExistsInList<FreezingStatusToken>(container)))
        { }

        public AmplificationSynergyContext BuildAmplificationContext(TokenAllocationContext context) =>
            new AmplificationSynergyContext(context.TokenContainerController, this, 0.25f,
                totalAmplification => slowMultiplier = 1f + totalAmplification);

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
