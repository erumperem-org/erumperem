using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// BUFF — Esquiva. Concede chance de evitar completamente um ataque.
    /// Cada stack adiciona dodgeChancePerStack à chance base.
    /// Sinergias:
    ///   - Amplification: StealthToken presente amplifica a chance de esquiva.
    ///   - Passive: enquanto ativo, aumenta passivamente a evasão geral da unidade por tick.
    /// Allocation: on-event.
    /// </summary>
    public class DodgeToken : TokenController, IAmplificationSynergy, IPassiveSynergy
    {
        public float dodgeChance { get; private set; }
        private float amplifierBonus    = 0f;
        private float passiveEvasionBonus = 0f;

        private const float dodgeChancePerStack  = 0.08f;
        private const float passiveBonusPerTick  = 0.01f;

        private readonly Action<float> applyEvasion;

        public HashSet<Type> amplificationSynergys { get; } = new() { typeof(StealthToken) };
        public HashSet<Type> passiveSynergys       { get; } = new() { typeof(StealthToken), typeof(BlindToken) };

        public DodgeToken(Action<float> applyEvasion, float baseDodge = 0.10f) : base(
            typeof(DodgeToken).Name,
            new LinearStackData(dodgeChancePerStack),
            new IOnEventTokenAllocation(null))
        {
            dodgeChance       = baseDodge;
            this.applyEvasion = applyEvasion;
        }

        public AmplificationSynergyContext BuildAmplificationContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this,
                amplifierPerStack: 0.1f,
                onAmplify: v => amplifierBonus = v);

        public PassiveSynergyContext BuildPassiveContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this,
                onPassiveTick: _ => passiveEvasionBonus += passiveBonusPerTick);

        public void ReverseSynergy(TokenContainerController tokenContainer)
        {
            amplifierBonus    = 0f;
            passiveEvasionBonus = 0f;
        }

        public bool CanApply(TokenAllocationContext context) =>
            TokenContainerController.HasAnyByTypes(context.TokenContainerController, amplificationSynergys);

        public float EffectiveDodge =>
            dodgeChance
            + ((LinearStackData)data.tokenStackingdata).stacks * dodgeChancePerStack
            + amplifierBonus
            + passiveEvasionBonus;

        public override void ExecuteTokenEffect()
        {
            applyEvasion?.Invoke(EffectiveDodge);
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                $"Dodge — effective chance {EffectiveDodge * 100f:F1}%");
            base.ExecuteTokenEffect();
        }
    }
}