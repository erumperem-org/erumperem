using System;
using System.Collections.Generic;
using Core.Tokens;

namespace Core.Tokens
{
    /// <summary>
    /// A transmutation buff that gradually converts poison into regeneration.
    /// Each tick, advances the conversion progress of any active <see cref="PoisonToken"/>
    /// by 25%. When progress reaches 1.0, the PoisonToken is removed and replaced
    /// by a <see cref="RegenToken"/> — representing alchemical purification.
    /// Progress is tracked per individual PoisonToken instance.
    /// Allocation style: event-based — triggered by a specific game event.
    /// </summary>
    public class AlchemyToken : TokenController, IConversionSynergy
    {
        public HashSet<Type> conversionSynergys { get; } = new HashSet<Type> { typeof(PoisonToken) };
        private readonly Dictionary<TokenController, float> progress = new();
        public AlchemyToken() : base(typeof(AlchemyToken).Name, new RefreshDurationStackData(5), new IOnEventTokenAllocation(null)) { }
        public ConversionSynergyContext BuildContext(TokenAllocationContext context) =>
            new ConversionSynergyContext(context.TokenContainerController, this, 0.25f,
                async (target, delta) =>
                {
                    if (!progress.ContainsKey(target)) progress[target] = 0f;
                    progress[target] += delta;

                    if (progress[target] >= 1f)
                    {
                        TokenContainerController.RemoveTokenFromContainer(context.TokenContainerController, target);
                        await TokenContainerController.AddTokenToContainer(new TokenAllocationContext(context.ownerName, context.TokenContainerController, new RegenToken()));
                        progress.Remove(target);
                    }
                });
        public override void ExecuteTokenEffect() => base.ExecuteTokenEffect();
    }
}
