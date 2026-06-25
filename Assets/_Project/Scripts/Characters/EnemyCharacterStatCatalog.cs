using System;
using System.Collections.Generic;
using UnityEngine;

namespace Erumperem.Characters
{
    [CreateAssetMenu(
        menuName = "Erumperem/Characters/Enemy Character Stat Catalog",
        fileName = "EnemyCharacterStatCatalog")]
    public sealed class EnemyCharacterStatCatalog : ScriptableObject
    {
        [SerializeField] private List<EnemyCharacterStatDefinition> definitions = new();

        public IReadOnlyList<EnemyCharacterStatDefinition> Definitions => definitions;

        public bool TryGetDefinition(string characterId, out EnemyCharacterStatDefinition definition)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(characterId) || definitions == null)
            {
                return false;
            }

            for (var definitionIndex = 0; definitionIndex < definitions.Count; definitionIndex++)
            {
                var candidate = definitions[definitionIndex];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.CharacterId))
                {
                    continue;
                }

                if (string.Equals(candidate.CharacterId, characterId, StringComparison.OrdinalIgnoreCase))
                {
                    definition = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
