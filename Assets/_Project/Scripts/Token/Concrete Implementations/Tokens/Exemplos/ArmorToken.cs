using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// Represents a physical armor buff applied on hit.
    /// When allocated, reacts with any <see cref="AcidToken"/> present in the same
    /// container — both tokens are consumed in the process (mutual cancellation).
    /// Scales linearly: each additional stack provides a proportional defense bonus.
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class ArmorToken : TokenController, ICancellationSynergy
    {
        public HashSet<Type> cancellationSynergys { get; } = new HashSet<Type> { typeof(AcidToken) };
        public ArmorToken() : base(typeof(ArmorToken).Name, new LinearStackData(0.1f), new IOnHitTokenAllocation()) { }
        public CancellationSynergyContext BuildCancellationContext(TokenAllocationContext context) => new CancellationSynergyContext(context.TokenContainerController, this);
        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
