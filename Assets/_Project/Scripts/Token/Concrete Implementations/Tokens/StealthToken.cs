using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// BUFF — Furtividade. A unidade não pode ser alvo direto enquanto ativo.
    /// Sinergias:
    ///   - Immunity: bloqueia TauntToken e BlindToken de serem alocados na unidade
    ///     furtiva (alvos invisíveis não provocam nem ficam cegos da mesma forma).
    ///   - Amplification: DodgeToken presente amplifica a eficácia do sigilo
    ///     (unidade que esquiva é ainda mais difícil de detectar).
    /// Quebrado automaticamente ao atacar (tratado externamente via RemoveTokenFromContainer).
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
            setUntargetable?.Invoke(true);
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                $"Stealth active — unit is untargetable (bonus {stealthBonus:F2}x)");
            base.ExecuteTokenEffect();
        }
    }
}
