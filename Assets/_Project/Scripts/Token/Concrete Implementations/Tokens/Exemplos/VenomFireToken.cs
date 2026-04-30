using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// Result token produced when <see cref="EmberToken"/> transforms with <see cref="PoisonToken"/>.
    /// Represents a toxic fire — a combined burn-and-poison condition with extended duration.
    /// Has no synergy behavior; its effect is defined entirely in ExecuteTokenEffect.
    /// Allocation style: on-hit — reused when reallocated directly.
    /// </summary>
    public class VenomFireToken : TokenController
    {
        public VenomFireToken() : base(
            typeof(VenomFireToken).Name,
            new RefreshDurationStackData(4),
            new IOnHitTokenAllocation())
        { }
    }
}
