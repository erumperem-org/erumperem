using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// An ice projectile token that reacts with bleeding wounds to form a frozen bleed.
    /// When allocated alongside <see cref="BleedToken"/>, both are consumed and replaced
    /// by a <see cref="FrostBleedToken"/> — a combined slow-and-bleed effect.
    /// Only applies when BleedToken is already present in the container.
    /// Allocation style: condition-based — requires BleedToken to already be active.
    /// </summary>
    public class IceShardToken : TokenController, ITransformationSynergy
    {
        public HashSet<Type> transformationSynergys { get; } = new HashSet<Type> { typeof(BleedToken) };

        public IceShardToken(TokenContainerController container) : base(
            typeof(IceShardToken).Name,
            new IndependentStackData(),
            new IOnConditionMetTokenAllocation(() => TokenContainerController.TokenTypeExistsInList<BleedToken>(container)))
        { }

        public TransformationSynergyContext BuildContext(TokenAllocationContext context) =>
            new TransformationSynergyContext(context.ownerName, context.TokenContainerController, this,
                new FrostBleedToken());

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
