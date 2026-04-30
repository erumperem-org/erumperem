using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// Evolved form of <see cref="AshToken"/>, produced when 3 Ash stacks are reached.
    /// Represents a full-blown inferno — a severe, long-duration burn with amplified damage.
    /// Has no synergy behavior; its effect is defined entirely in ExecuteTokenEffect.
    /// Allocation style: on-hit — reused when reallocated directly.
    /// </summary>
    public class InfernoToken : TokenController
    {
        public InfernoToken() : base(
            typeof(InfernoToken).Name,
            new RefreshDurationStackData(5),
            new IOnHitTokenAllocation())
        { }
    }
}
