using System.Collections.Generic;
using Game.Core.Abstractions;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Catálogo de definições visuais com pick ponderado via <see cref="IRandomSource"/> (mesma seed do combate).
    /// </summary>
    [CreateAssetMenu(menuName = "Erumperem/Combat/Enemy Visual Spawn Catalog", fileName = "EnemyVisualSpawnCatalog")]
    public sealed class EnemyVisualSpawnCatalog : ScriptableObject
    {
        [Tooltip("Definições válidas (prefab não nulo, peso maior que zero).")]
        [SerializeField] private List<EnemyVisualDefinition> definitions = new();

        public IReadOnlyList<EnemyVisualDefinition> Definitions => definitions;

        /// <summary>Escolhe uma definição por peso; devolve false se não houver entradas válidas.</summary>
        public bool TryPickDefinition(IRandomSource randomSource, out EnemyVisualDefinition pickedDefinition)
        {
            pickedDefinition = null;
            if (definitions == null || definitions.Count == 0)
            {
                return false;
            }

            var totalWeight = 0f;
            for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                var candidate = definitions[definitionIndex];
                if (candidate == null || candidate.battlePrefab == null || candidate.spawnWeight <= 0f)
                {
                    continue;
                }

                totalWeight += candidate.spawnWeight;
            }

            if (totalWeight <= 0f)
            {
                return false;
            }

            var roll = (float)(randomSource.NextDouble() * totalWeight);
            EnemyVisualDefinition lastValidDefinition = null;
            for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                var candidate = definitions[definitionIndex];
                if (candidate == null || candidate.battlePrefab == null || candidate.spawnWeight <= 0f)
                {
                    continue;
                }

                lastValidDefinition = candidate;
                roll -= candidate.spawnWeight;
                if (roll <= 0f)
                {
                    pickedDefinition = candidate;
                    return true;
                }
            }

            pickedDefinition = lastValidDefinition;
            return pickedDefinition != null;
        }
    }
}
