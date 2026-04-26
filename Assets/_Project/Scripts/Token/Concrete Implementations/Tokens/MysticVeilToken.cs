using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A magical ward that dampens the potency of curses.
    /// When active alongside <see cref="CurseToken"/>, registers a resistance factor
    /// of 0.4 — meaning CurseToken should reduce its effect by 60% when this is present.
    /// Convention: CurseToken consults the container for IResistanceSynergy tokens
    /// targeting its type before applying its debuff value.
    /// Allocation style: on-hit — applied whenever the attack connects.
    /// </summary>
    public class MysticVeilToken : TokenController, IResistanceSynergy
    {
        public HashSet<Type> resistanceSynergys { get; } = new HashSet<Type> { typeof(CurseToken) };
        public float ResistanceFactor => 0.4f;

        public MysticVeilToken() : base(
            typeof(MysticVeilToken).Name,
            new RefreshDurationStackData(3),
            new IOnHitTokenAllocation())
        { }

        public ResistanceSynergyContext BuildContext(TokenAllocationContext context) =>
            new ResistanceSynergyContext(context.TokenContainerController, this, ResistanceFactor);

        public void ApplyResistanceSynergy(ResistanceSynergyContext context) { }

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
