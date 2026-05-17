using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// DOT — Natureza/Corrosão. Causa dano biológico a cada turno com diminishing returns por stack.
    /// Múltiplas aplicações do mesmo Blight enfraquecem cada stack adicional
    /// (DiminishingStackData, fator 0.15).
    /// Sinergias:
    ///   - Inversion: se um efeito de purificação (RegenToken) estiver ativo,
    ///     o Blight inverte — ao invés de causar dano, drena cura da praga.
    /// Allocation: on-hit.
    /// </summary>
    public class BlightToken : TokenController, IInversionSynergy
    {
        private readonly Action<float> applyDamage;
        private readonly Action<float> applyHealDrain;
        private bool inverted = false;

        public HashSet<Type> inversionSynergys { get; } = new() { typeof(RegenToken) };

        public BlightToken(Action<float> applyDamage, Action<float> applyHealDrain) : base(
            typeof(BlightToken).Name,
            new DiminishingStackData(0.15f),
            new IOnHitTokenAllocation())
        {
            this.applyDamage     = applyDamage;
            this.applyHealDrain  = applyHealDrain;
        }

        public InversionSynergyContext BuildInversionContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this,
                onInvert:  () => inverted = true,
                onRestore: () => inverted = false);

        public override void ExecuteTokenEffect()
        {
            var stackData = (DiminishingStackData)data.tokenStackingdata;
            float factor  = MathF.Pow(1f - stackData.decreaseFactor, stackData.stacks - 1);
            float value   = 8f * factor;

            if (inverted)
            {
                applyHealDrain?.Invoke(value);
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                    $"Blight (inverted) — drains {value:F1} healing");
            }
            else
            {
                applyDamage?.Invoke(value);
                LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                    $"Blight tick — {value:F1} damage (stack factor {factor:F2})");
            }
            base.ExecuteTokenEffect();
        }
    }
}
