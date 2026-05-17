using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// DOT — Físico. Causa dano por sangramento com stacks independentes (cada hit abre uma ferida).
    /// Cada instância tem sua própria duração e valor — não se fundem.
    /// Sinergias:
    ///   - Absorption: consome BlockToken/BlockPlusToken presentes ao ser alocado,
    ///     ignorando a mitigação (sangramento rasga a armadura) e ganhando +1 de dano por bloco consumido.
    /// Allocation: on-hit.
    /// </summary>
    public class BleedToken : TokenController, IAbsorptionSynergy
    {
        private readonly Action<float> applyDamage;
        private float bonusDamage = 0f;
        private readonly float baseDamage;

        public HashSet<Type> absorptionSynergys { get; } = new() { typeof(BlockToken), typeof(BlockPlusToken) };

        public BleedToken(Action<float> applyDamage, float baseDamage = 4f) : base(
            typeof(BleedToken).Name,
            new IndependentStackData(),
            new IOnHitTokenAllocation())
        {
            this.applyDamage = applyDamage;
            this.baseDamage  = baseDamage;
        }

        public AbsorptionSynergyContext BuildAbsorptionContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this,
                onAbsorb: _ => bonusDamage += 1f);

        public override void ExecuteTokenEffect()
        {
            float total = baseDamage + bonusDamage;
            applyDamage?.Invoke(total);
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                $"Bleed tick — {total:F1} damage (base {baseDamage} + {bonusDamage} armor-break bonus)");
            base.ExecuteTokenEffect();
        }
    }
}
