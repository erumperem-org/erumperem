using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A fury buff that grows stronger with each bleeding wound on the target.
    /// For every active <see cref="BleedToken"/> in the container, the damage
    /// multiplier increases by 0.15 — representing rage fueled by drawn blood.
    /// The computed multiplier is stored in <see cref="currentMultiplier"/> for
    /// use during ExecuteTokenEffect.
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class RageToken : TokenController, IAmplificationSynergy
    {
        public HashSet<Type> amplificationSynergys { get; } = new HashSet<Type> { typeof(BleedToken) };
        public float currentMultiplier = 1f;

        public RageToken() : base(
            typeof(RageToken).Name,
            new LinearStackData(0.2f),
            new IOnHitTokenAllocation())
        { }

        public AmplificationSynergyContext BuildAmplificationContext(TokenAllocationContext context) =>
            new AmplificationSynergyContext(context.TokenContainerController, this, 0.15f,
                totalAmplification => currentMultiplier = 1f + totalAmplification);

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
