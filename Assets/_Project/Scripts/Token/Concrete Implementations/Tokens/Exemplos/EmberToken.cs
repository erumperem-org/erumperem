using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A smoldering token that reacts with poison to ignite a venomous fire.
    /// When allocated alongside <see cref="PoisonToken"/>, both are consumed and replaced
    /// by a <see cref="VenomFireToken"/> — a combined burn-and-poison effect.
    /// Only applies when PoisonToken is already present in the container.
    /// Allocation style: condition-based — requires PoisonToken to already be active.
    /// </summary>
    public class EmberToken : TokenController, ITransformationSynergy
    {
        public HashSet<Type> transformationSynergys { get; } = new HashSet<Type> { typeof(PoisonToken) };

        public EmberToken(TokenContainerController container) : base(
            typeof(EmberToken).Name,
            new IndependentStackData(),
            new IOnConditionMetTokenAllocation(() => TokenContainerController.TokenTypeExistsInList<PoisonToken>(container)))
        { }

        public TransformationSynergyContext BuildTransformationContext(TokenAllocationContext context) =>
            new TransformationSynergyContext(context.ownerName, context.TokenContainerController, this,
                new VenomFireToken());

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
