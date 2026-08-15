using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// GRADUAL CONSUMER — progressively transforms a target token over multiple ticks.
// CanApply: requires at least one convertible token present.
// Reverseable: clears in-progress conversion so the target token is no longer being transformed.
namespace Core.Tokens
{
    public interface IConversionSynergy : ITokenSynergy, IReverseableSynergy
    {
        HashSet<Type> conversionSynergys { get; }
        ConversionSynergyContext BuildConversionContext(TokenAllocationContext context);

        bool ITokenSynergy.CanApply(TokenAllocationContext context) => TokenContainerController.HasAnyByTypes(context.TokenContainerController, conversionSynergys);

        public async Task ApplyConversionSynergy(ConversionSynergyContext context)
        {
            var targetTokens = TokenContainerController.GetTokensByTypes(
                context.TokenContainerController,
                conversionSynergys);
            foreach (TokenController targetToken in targetTokens)
            {
                if (context.onConversionTick != null)
                    await context.onConversionTick(targetToken, context.tickDelta);
            }
        }

        // Calls onCancelConversion so the implementor clears accumulated progress
        // for each target that was being gradually transformed.
        void IReverseableSynergy.ReverseSynergy(TokenContainerController tokenContainer)
        {
            ConversionSynergyContext conversionContext = BuildConversionContext(
                new TokenAllocationContext(
                    string.Empty,
                    tokenContainer,
                    (TokenController)this));
            var targetTokens =
                TokenContainerController.GetTokensByTypes(tokenContainer, conversionSynergys);
            foreach (TokenController targetToken in targetTokens)
                conversionContext.onCancelConversion?.Invoke(targetToken);
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
        public Func<TokenController, float, Task> onConversionTick;
        // Called once on removal for each in-progress target; implementor clears its progress entry.
        public Action<TokenController> onCancelConversion;

        public ConversionSynergyContext(
            TokenContainerController TokenContainerController,
            TokenController self,
            float tickDelta,
            Func<TokenController, float, Task> onConversionTick,
            Action<TokenController> onCancelConversion = null)
        {
            this.TokenContainerController = TokenContainerController;
            this.self = self;
            this.tickDelta = tickDelta;
            this.onConversionTick = onConversionTick;
            this.onCancelConversion = onCancelConversion;
        }
    }
}