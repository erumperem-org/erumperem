using System;
using System.Collections.Generic;
using System.Linq;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A damage-over-time token representing a poisoned condition.
    /// When a second PoisonToken is allocated to the same container, its
    /// <see cref="damagePerTick"/> accumulates into the existing instance rather
    /// than creating a separate entry — the poison intensifies.
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class PoisonToken : TokenController, IAdditiveSynergy
    {
        public HashSet<Type> additiveSynergys { get; } = new HashSet<Type> { typeof(PoisonToken) };
        public int damagePerTick = 5;

        public PoisonToken() : base(
            typeof(PoisonToken).Name,
            new LinearStackData(0.1f),
            new IOnHitTokenAllocation())
        { }

        public AdditiveSynergyContext BuildContext(TokenAllocationContext context) =>
            new AdditiveSynergyContext(context.TokenContainerController, this);

        public void ApplyAdditiveSynergy(AdditiveSynergyContext context)
        {
            var existing = TokenContainerController.GetOtherToken<PoisonToken>(context.TokenContainerController, this);
            if (existing != null)
                existing.damagePerTick += damagePerTick;
        }

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
