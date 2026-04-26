using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A rebirth buff that rises from consumed ash stacks.
    /// When allocated alongside <see cref="AshToken"/>, absorbs it and accumulates
    /// its stacks as internal power — representing a phoenix rising stronger from the ashes.
    /// Only applies when AshToken is already present in the container.
    /// Allocation style: condition-based — requires AshToken to already be active.
    /// </summary>
    public class PhoenixToken : TokenController, IAbsorptionSynergy
    {
        public HashSet<Type> absorptionSynergys { get; } = new HashSet<Type> { typeof(AshToken) };
        private int absorbedStacks = 0;

        public PhoenixToken(TokenContainerController container) : base(
            typeof(PhoenixToken).Name,
            new LinearStackData(0.5f),
            new IOnConditionMetTokenAllocation(() => TokenContainerController.TokenTypeExistsInList<AshToken>(container)))
        { }

        public AbsorptionSynergyContext BuildContext(TokenAllocationContext context) =>
            new AbsorptionSynergyContext(context.TokenContainerController, this, absorbed =>
            {
                if (absorbed.data.tokenStackingdata is LinearStackData s)
                    absorbedStacks += s.stacks;
            });

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
