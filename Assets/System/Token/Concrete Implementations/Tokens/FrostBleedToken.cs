using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// Result token produced when <see cref="IceShardToken"/> transforms with <see cref="BleedToken"/>.
    /// Represents a frostbitten wound — a combined slow-and-bleed condition with extended duration.
    /// Has no synergy behavior; its effect is defined entirely in ExecuteTokenEffect.
    /// Allocation style: on-hit — reused when reallocated directly.
    /// </summary>
    public class FrostBleedToken : TokenController
    {
        public FrostBleedToken() : base(
            typeof(FrostBleedToken).Name,
            new RefreshDurationStackData(4),
            new IOnHitTokenAllocation())
        { }
    }
}
