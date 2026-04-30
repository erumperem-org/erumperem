using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A physical hardening buff that reduces incoming bleed severity.
    /// When active alongside <see cref="BleedToken"/>, registers a resistance factor
    /// of 0.6 — meaning BleedToken should reduce its effect by 40% when this is present.
    /// Convention: BleedToken consults the container for IResistanceSynergy tokens
    /// targeting its type before applying its damage value.
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class StoneToken : TokenController, IResistanceSynergy
    {
        public HashSet<Type> resistanceSynergys { get; } = new HashSet<Type> { typeof(BleedToken) };
        public float ResistanceFactor => 0.6f;

        public StoneToken() : base(
            typeof(StoneToken).Name,
            new RefreshDurationStackData(2),
            new IOnHitTokenAllocation())
        { }

        public ResistanceSynergyContext BuildResistanceContext(TokenAllocationContext context) =>
            new ResistanceSynergyContext(context.TokenContainerController, this, ResistanceFactor);

        public void ApplyResistanceSynergy(ResistanceSynergyContext context) { }

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
