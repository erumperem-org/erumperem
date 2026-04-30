using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A corruption debuff that gradually poisons regenerative effects.
    /// Each tick, advances the corruption progress of any active <see cref="RegenToken"/>
    /// by 25%. When progress reaches 1.0, the RegenToken is removed and replaced
    /// by a <see cref="PoisonToken"/> — representing healing twisted into harm.
    /// Progress is tracked per individual RegenToken instance.
    /// Allocation style: event-based — triggered by a specific game event.
    /// </summary>
    public class CorruptionToken : TokenController, IConversionSynergy
    {
        public HashSet<Type> conversionSynergys { get; } = new HashSet<Type> { typeof(RegenToken) };
        private readonly Dictionary<TokenController, float> progress = new();

        public CorruptionToken() : base(
            typeof(CorruptionToken).Name,
            new RefreshDurationStackData(5),
            new IOnEventTokenAllocation(null))
        { }

        public ConversionSynergyContext BuildConversionContext(TokenAllocationContext context) =>
            new ConversionSynergyContext(context.TokenContainerController, this, 0.25f,
                async (target, delta) =>
                {
                    if (!progress.ContainsKey(target)) progress[target] = 0f;
                    progress[target] += delta;

                    if (progress[target] >= 1f)
                    {
                        TokenContainerController.RemoveTokenFromContainer(context.TokenContainerController, target);
                        await TokenContainerController.AddTokenToContainer(new TokenAllocationContext(context.ownerName, context.TokenContainerController, new PoisonToken()));
                        progress.Remove(target);
                    }
                });

        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
