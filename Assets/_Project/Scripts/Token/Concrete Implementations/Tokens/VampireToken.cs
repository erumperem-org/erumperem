using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A vampiric buff that feeds on bleeding wounds.
    /// When allocated alongside <see cref="BleedToken"/>, absorbs it and converts
    /// the drained duration into bonus duration for itself.
    /// Only applies when BleedToken is already present in the container.
    /// Allocation style: condition-based — requires BleedToken to already be active.
    /// </summary>
    public class VampireToken : TokenController, IAbsorptionSynergy
    {
        public HashSet<Type> absorptionSynergys { get; } = new HashSet<Type> { typeof(BleedToken) };
        private int bonusDuration = 0;

        public VampireToken(TokenContainerController container) : base(
            typeof(VampireToken).Name,
            new RefreshDurationStackData(3),
            new IOnConditionMetTokenAllocation(() => TokenContainerController.TokenTypeExistsInList<BleedToken>(container)))
        { }

        public AbsorptionSynergyContext BuildContext(TokenAllocationContext context) =>
            new AbsorptionSynergyContext(context.TokenContainerController, this, absorbed =>
            {
                if (absorbed.data.tokenStackingdata is RefreshDurationStackData s)
                    bonusDuration += s.currentDuration;
            });

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
