using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A heal-over-time token representing a regenerating condition.
    /// When a second RegenToken is allocated to the same container, its
    /// <see cref="healPerTick"/> accumulates into the existing instance rather
    /// than creating a separate entry — the regeneration compounds.
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class RegenToken : TokenController, IAdditiveSynergy
    {
        public HashSet<Type> additiveSynergys { get; } = new HashSet<Type> { typeof(RegenToken) };
        public int healPerTick = 8;

        public RegenToken() : base(
            typeof(RegenToken).Name,
            new LinearStackData(0.1f),
            new IOnHitTokenAllocation())
        { }

        public AdditiveSynergyContext BuildAdditiveContext(TokenAllocationContext context) =>
            new AdditiveSynergyContext(context.TokenContainerController, this);

        public void ApplyAdditiveSynergy(AdditiveSynergyContext context)
        {
            var existing = TokenContainerController.GetOtherToken<RegenToken>(context.TokenContainerController, this);
            if (existing != null)
                existing.healPerTick += healPerTick;
        }

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
