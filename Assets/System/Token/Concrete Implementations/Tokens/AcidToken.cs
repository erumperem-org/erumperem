using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// Represents a corrosive acid effect applied on hit.
    /// When allocated, reacts with any <see cref="ArmorToken"/> present in the same
    /// container — both tokens are consumed in the process (mutual cancellation).
    /// Useful for bypass mechanics: acid counters armor, armor absorbs acid.
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class AcidToken : TokenController, ICancellationSynergy
    {
        public HashSet<Type> cancellationSynergys { get; } = new HashSet<Type> { typeof(ArmorToken) };

        public AcidToken() : base(
            typeof(AcidToken).Name,
            new IndependentStackData(),
            new IOnHitTokenAllocation())
        { }

        public CancellationSynergyContext BuildContext(TokenAllocationContext context) =>
            new CancellationSynergyContext(context.TokenContainerController, this);

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
