using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// DOT — Fogo. Causa dano a cada início de turno baseado no número de stacks.
    /// Cada reaplicação aumenta os stacks linearmente (fator 0.1 por stack).
    /// Sinergias:
    ///   - Bleed presente → Amplification: dano aumentado (cauterização falha, ferida arde).
    ///   - Immunity: bloqueia FreezingStatusToken de ser alocado enquanto ativo.
    /// Allocation: on-hit.
    /// </summary>
    public class BurnToken : TokenController, IAmplificationSynergy, IImmunitySynergy
    {
        private readonly Action<float> applyDamage;
        private float currentDamageMultiplier = 1f;

        public HashSet<Type> amplificationSynergys { get; } = new() { typeof(BleedToken) };
        public HashSet<Type> immunitySynergys      { get; } = new() { typeof(FreezingStatusToken) };

        public BurnToken(Action<float> applyDamage, float baseDamage = 5f) : base(
            typeof(BurnToken).Name,
            new LinearStackData(0.1f),
            new IOnHitTokenAllocation())
        {
            this.applyDamage = applyDamage;
        }

        // ── Amplification ────────────────────────────────────────────────────
        public AmplificationSynergyContext BuildAmplificationContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this,
                amplifierPerStack: 0.5f,
                onAmplify: factor => currentDamageMultiplier = (factor == 0f) ? 1f : 1f + factor);

        // ── Immunity ─────────────────────────────────────────────────────────
        public ImmunitySynergyContext BuildImmunityContext(TokenAllocationContext ctx) =>
            new(ctx.TokenContainerController, this);

        // ── Effect ───────────────────────────────────────────────────────────
        public override void ExecuteTokenEffect()
        {
            var stacks = ((LinearStackData)data.tokenStackingdata).stacks;
            var damage  = stacks * currentDamageMultiplier;
            applyDamage?.Invoke(damage);
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                $"Burn tick — {damage:F1} damage ({stacks} stacks × {currentDamageMultiplier:F2})");
            base.ExecuteTokenEffect();
        }
    }
}
