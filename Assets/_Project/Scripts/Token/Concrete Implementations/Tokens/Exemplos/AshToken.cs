using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A residual burn token that accumulates independent stacks.
    /// When 3 or more AshToken instances are active in the same container,
    /// all stacks are consumed and replaced by an <see cref="InfernoToken"/> —
    /// representing smoldering embers that have reignited into a full inferno.
    /// Allocation style: on-hit — a new independent instance is added per hit.
    /// </summary>
    public class AshToken : TokenController, IEvolutionSynergy
    {
        public HashSet<Type> evolutionSynergys { get; } = new HashSet<Type> { typeof(AshToken) };
        public int evolutionThreshold { get; } = 3;

        public AshToken() : base(
            typeof(AshToken).Name,
            new IndependentStackData(),
            new IOnHitTokenAllocation())
        { }

        public EvolutionSynergyContext BuildEvolutionContext(TokenAllocationContext context) =>
            new EvolutionSynergyContext(context.ownerName, context.TokenContainerController, this,
                new InfernoToken());

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
