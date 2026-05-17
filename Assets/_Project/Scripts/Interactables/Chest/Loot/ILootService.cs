using System.Collections.Generic;
using Core.Exploration.Interactables.Chest;

namespace Services.Loot
{
    /// <summary>
    /// Contrato do serviço de geração de loot.
    /// Desacopla o baú da implementação concreta, permitindo
    /// substituição (mock em testes, variantes de dificuldade, etc.).
    /// </summary>
    public interface ILootService
    {
        /// <summary>
        /// Gera os itens para um baú a partir de uma <see cref="LootTable"/>.
        /// </summary>
        /// <param name="lootTable">
        ///     Tabela que define entradas, pesos e capacidade máxima.
        ///     Pode ser <c>null</c>; nesse caso retorna dicionário vazio.
        /// </param>
        /// <param name="context">
        ///     Informações do baú no momento da abertura
        ///     (nome, posição, etc.) usadas apenas para logging.
        /// </param>
        /// <returns>
        ///     Dicionário de <see cref="IStorageable"/> → quantidade sorteada.
        ///     Nunca retorna <c>null</c>.
        /// </returns>
        IReadOnlyDictionary<IStorageable, int> GenerateLoot(LootTable lootTable, LootRequestContext context);
    }
}
