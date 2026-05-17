using System;
using UnityEngine;

namespace Core.Exploration.Interactables.Chest
{
    /// <summary>
    /// Entrada individual na tabela de loot.
    /// Contém o item sorteável, seu peso relativo e o range de quantidade.
    /// </summary>
    [Serializable]
    public struct LootEntry
    {
        [Tooltip("Item que pode ser sorteado. Deve implementar IStorageable.")]
        public UnityEngine.Object item;

        [Tooltip("Peso relativo do sorteio. Valores maiores aumentam a chance proporcional.")]
        [Min(0f)]
        public float weight;

        [Tooltip("Quantidade mínima sorteada (inclusivo).")]
        [Min(0)]
        public int minQuantity;

        [Tooltip("Quantidade máxima sorteada (inclusivo).")]
        [Min(1)]
        public int maxQuantity;

        /// <summary>
        /// Retorna o item como IStorageable. Nulo se o asset não implementar a interface.
        /// </summary>
        public IStorageable Storageable => item as IStorageable;

        /// <summary>
        /// Valida que o asset referenciado implementa IStorageable e que o range é coerente.
        /// </summary>
        public bool IsValid => Storageable != null && weight > 0f && maxQuantity >= minQuantity;
    }
}
