using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A wound-over-time token that accumulates independent stacks.
    /// When 3 or more BleedToken instances are active in the same container,
    /// all stacks are consumed and replaced by a <see cref="HemorrhageToken"/> —
    /// representing a wound that has escalated into critical blood loss.
    /// Allocation style: on-hit — a new independent instance is added per hit.
    /// </summary>
    public class BleedToken : TokenController, IEvolutionSynergy
    {
        public HashSet<Type> evolutionSynergys { get; } = new HashSet<Type> { typeof(BleedToken) };
        public int evolutionThreshold { get; } = 3;

        public BleedToken() : base(
            typeof(BleedToken).Name,
            new IndependentStackData(),
            new IOnHitTokenAllocation())
        { }

        public EvolutionSynergyContext BuildContext(TokenAllocationContext context) =>
            new EvolutionSynergyContext(context.ownerName, context.TokenContainerController, this,
                new HemorrhageToken());

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
