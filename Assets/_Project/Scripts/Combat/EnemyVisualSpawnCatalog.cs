using System;
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
        public bool TryPickDefinition(IRandomSource randomSource, out EnemyVisualDefinition pickedDefinition) =>
            TryPickDefinitionExcludingCharacterStatIds(randomSource, Array.Empty<string>(), out pickedDefinition);

        /// <summary>
        /// Pick ponderado omitindo entradas cujo <see cref="EnemyVisualDefinition.ResolveCharacterStatId"/>
        /// coincide com algum id em <paramref name="excludedCharacterStatIds"/> (ex.: HorseBoss em encontros aleatórios).
        /// </summary>
        public bool TryPickDefinitionExcludingCharacterStatIds(
            IRandomSource randomSource,
            IReadOnlyList<string> excludedCharacterStatIds,
            out EnemyVisualDefinition pickedDefinition)
        {
            pickedDefinition = null;
            if (definitions == null || definitions.Count == 0)
            {
                return false;
            }

            var totalWeight = 0f;
            EnemyVisualDefinition lastEligibleDefinition = null;
            for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                var candidate = definitions[definitionIndex];
                if (!IsEligibleForWeightedPick(candidate, excludedCharacterStatIds))
                {
                    continue;
                }

                lastEligibleDefinition = candidate;
                totalWeight += candidate.spawnWeight;
            }

            if (totalWeight <= 0f || lastEligibleDefinition == null)
            {
                return false;
            }

            var roll = (float)(randomSource.NextDouble() * totalWeight);
            for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                var candidate = definitions[definitionIndex];
                if (!IsEligibleForWeightedPick(candidate, excludedCharacterStatIds))
                {
                    continue;
                }

                lastEligibleDefinition = candidate;
                roll -= candidate.spawnWeight;
                if (roll <= 0f)
                {
                    pickedDefinition = candidate;
                    return true;
                }
            }

            pickedDefinition = lastEligibleDefinition;
            return pickedDefinition != null;
        }

        private static bool IsEligibleForWeightedPick(
            EnemyVisualDefinition candidate,
            IReadOnlyList<string> excludedCharacterStatIds)
        {
            if (candidate == null || candidate.battlePrefab == null || candidate.spawnWeight <= 0f)
            {
                return false;
            }

            if (excludedCharacterStatIds == null || excludedCharacterStatIds.Count == 0)
            {
                return true;
            }

            var candidateCharacterStatId = candidate.ResolveCharacterStatId();
            for (var excludedIndex = 0; excludedIndex < excludedCharacterStatIds.Count; excludedIndex++)
            {
                var excludedCharacterStatId = excludedCharacterStatIds[excludedIndex];
                if (string.IsNullOrWhiteSpace(excludedCharacterStatId))
                {
                    continue;
                }

                if (string.Equals(
                        candidateCharacterStatId,
                        excludedCharacterStatId.Trim(),
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
