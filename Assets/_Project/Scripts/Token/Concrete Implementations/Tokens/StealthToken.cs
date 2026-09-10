using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;

namespace Core.Tokens
{
    /// <summary>
    /// BUFF — Furtividade. Skills that target this unit have -40% accuracy.
    /// Loses 1 stack at end of turn (decay handled by Core combat status ticker).
    /// Sinergias:
    ///   - Immunity: bloqueia TauntToken e BlindToken de serem alocados na unidade furtiva.
    ///   - Amplification: DodgeToken presente amplifica a eficácia do sigilo.
    /// Allocation: on-event.
    /// </summary>
    public class StealthToken : TokenController, IImmunitySynergy, IAmplificationSynergy
    {
        private readonly Action<bool> setUntargetable;
        private float stealthBonus = 1f;

        public HashSet<Type> immunitySynergys     { get; } = new() { typeof(TauntToken), typeof(BlindToken) };
        public HashSet<Type> amplificationSynergys { get; } = new() { typeof(DodgeToken) };

        public StealthToken(TokenContainerController container, Action<bool> setUntargetable) : base(
            typeof(StealthToken).Name,
            new RefreshDurationStackData(3),
            new IOnConditionMetTokenAllocation(
                () => !TokenContainerController.TokenTypeExistsInList<StealthToken>(container)))
        {
            this.setUntargetable = setUntargetable;
        }

        public ImmunitySynergyContext BuildImmunityContext(TokenAllocationContext context) =>
            new(context.TokenContainerController, this);

        public AmplificationSynergyContext BuildAmplificationContext(TokenAllocationContext ctx) =>
            new(ctx.TokenContainerController, this,
                amplifierPerStack: 0.2f,
                onAmplify: v => stealthBonus = (v == 0f) ? 1f : 1f + v);

        public override void ExecuteTokenEffect()
        {
            // Stealth no longer makes the unit untargetable; Core applies -40% accuracy instead.
            setUntargetable?.Invoke(false);
            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"Stealth active — attackers have -40% accuracy (bonus {stealthBonus:F2}x)", LogCategory.Combat);
            base.ExecuteTokenEffect();
        }
    }
}
