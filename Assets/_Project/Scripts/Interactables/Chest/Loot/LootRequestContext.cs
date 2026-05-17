using UnityEngine;

namespace Services.Loot
{
    /// <summary>
    /// Dados imutáveis do baú no momento da solicitação de geração de loot.
    /// Passado ao <see cref="ILootService"/> para logging e futura extensibilidade
    /// (ex.: modificadores de dificuldade, posição, cena atual).
    /// </summary>
    public sealed class LootRequestContext
    {
        /// <summary>Nome do GameObject do baú, usado em mensagens de log.</summary>
        public string ChestName { get; }

        /// <summary>Posição do baú na cena, disponível para regras geográficas futuras.</summary>
        public Vector3 WorldPosition { get; }

        public LootRequestContext(string chestName, Vector3 worldPosition)
        {
            ChestName     = chestName;
            WorldPosition = worldPosition;
        }
    }
}
