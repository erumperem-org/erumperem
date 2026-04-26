using System;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;
using Services.DebugUtilities.Console;
using System.Threading.Tasks;

// GRADUAL CONSUMER — progressively transforms a target token over multiple ticks.
// CanApply: requires at least one convertible token present.
namespace Core.Tokens
{
    public interface IConversionSynergy : ITokenSynergy
    {
        HashSet<Type> conversionSynergys { get; }
        ConversionSynergyContext BuildContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, conversionSynergys);
        public void ApplyConversionSynergy(ConversionSynergyContext context)
        {
            var targets = TokenContainerController.GetTokensByTypes(context.TokenContainerController, conversionSynergys);
            foreach (var target in targets)
                context.onConversionTick?.Invoke(target, context.tickDelta);
        }
    }

    [Serializable]
    public struct ConversionSynergyContext
    {
        public TokenContainerController TokenContainerController;
        public TokenController self;
        // Progress applied per tick (0–1). Implementor tracks cumulative progress.
        public float tickDelta;
        // Called each tick with the target and delta; implementor removes target and
        // adds result token when cumulative progress reaches 1.
        public Action<TokenController, float> onConversionTick;

        public ConversionSynergyContext(TokenContainerController TokenContainerController, TokenController self, float tickDelta, Action<TokenController, float> onConversionTick)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.tickDelta = tickDelta;
            this.onConversionTick = onConversionTick;
        }
    }
}
