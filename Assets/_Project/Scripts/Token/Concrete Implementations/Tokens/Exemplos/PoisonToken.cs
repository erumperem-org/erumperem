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
        private const int DefaultDamagePerTick = 5;

        private readonly Action<int> applyDamage;

        public HashSet<Type> additiveSynergys { get; } = new HashSet<Type> { typeof(PoisonToken) };
        public int damagePerTick = DefaultDamagePerTick;

        public PoisonToken() : this(null)
        { }

        public PoisonToken(
            Action<int> applyDamage,
            int damagePerTick = DefaultDamagePerTick) : base(
            typeof(PoisonToken).Name,
            new LinearStackData(0.1f),
            new IOnHitTokenAllocation())
        {
            this.applyDamage = applyDamage;
            this.damagePerTick = damagePerTick;
        }

        public AdditiveSynergyContext BuildAdditiveContext(TokenAllocationContext context) =>
            new AdditiveSynergyContext(context.TokenContainerController, this);

        public void ApplyAdditiveSynergy(AdditiveSynergyContext context)
        {
            PoisonToken existingPoisonToken =
                TokenContainerController.GetOtherToken<PoisonToken>(
                    context.TokenContainerController,
                    this);
            if (existingPoisonToken != null)
                existingPoisonToken.damagePerTick += damagePerTick;
        }

        public override void ExecuteTokenEffect()
        {
            applyDamage?.Invoke(damagePerTick);
            base.ExecuteTokenEffect();
        }
    }
}
