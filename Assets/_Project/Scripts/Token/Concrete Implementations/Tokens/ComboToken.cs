using System;
using System.Collections.Generic;
using Core.Tokens;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;

namespace Core.Tokens
{
    /// <summary>
    /// MARCADOR — Combo. Acumula cargas que ativam bônus de habilidades (SkillDefinition.ComboBonus).
    /// Stacks independentes: cada habilidade que gera combo adiciona uma instância.
    /// Sinergias:
    ///   - Evolution: ao atingir 3 instâncias de ComboToken, evolui para ComboFinisherToken
    ///     (golpe finalizador liberado).
    ///   - Immunity: BlindToken bloqueia este token (unidade cega não combina ataques).
    /// Allocation: on-hit.
    /// </summary>
    public class ComboToken : TokenController, IEvolutionSynergy
    {
        private readonly Func<TokenContainerController, ComboFinisherToken> finisherFactory;

        public HashSet<Type> evolutionSynergys { get; } = new() { typeof(ComboToken) };
        public int evolutionThreshold { get; } = 3;

        public ComboToken() : base(
            typeof(ComboToken).Name,
            new IndependentStackData(),
            new IOnHitTokenAllocation())
        {
        }

        public EvolutionSynergyContext BuildEvolutionContext(TokenAllocationContext context) =>
            new(context.ownerName, context.TokenContainerController, this,
                evolvedToken: finisherFactory(context.TokenContainerController));

        public override void ExecuteTokenEffect()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug, LogCategory.Combat,
                "Combo marker added — combo bonus eligible for skill activation");
            base.ExecuteTokenEffect();
        }
    }
}
