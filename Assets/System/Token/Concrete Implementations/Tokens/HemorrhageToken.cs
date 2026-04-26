using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// Evolved form of <see cref="BleedToken"/>, produced when 3 Bleed stacks are reached.
    /// Represents a critical hemorrhage — a severe, long-duration bleed with amplified damage.
    /// Has no synergy behavior; its effect is defined entirely in ExecuteTokenEffect.
    /// Allocation style: on-hit — reused when reallocated directly.
    /// </summary>
    public class HemorrhageToken : TokenController
    {
        public HemorrhageToken() : base(
            typeof(HemorrhageToken).Name,
            new RefreshDurationStackData(5),
            new IOnHitTokenAllocation())
        { }
    }
}
