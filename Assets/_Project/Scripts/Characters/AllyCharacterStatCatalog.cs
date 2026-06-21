using System;
using System.Collections.Generic;
using UnityEngine;

namespace Erumperem.Characters
{
    [CreateAssetMenu(
        menuName = "Erumperem/Characters/Ally Character Stat Catalog",
        fileName = "AllyCharacterStatCatalog")]
    public sealed class AllyCharacterStatCatalog : ScriptableObject
    {
        private const PlayableCharacterState DefaultExplorationState = PlayableCharacterState.Resting;

        [SerializeField] private List<AllyCharacterStatDefinition> definitions = new();

        public IReadOnlyList<AllyCharacterStatDefinition> Definitions => definitions;

        public bool TryGetDefinition(string characterId, out AllyCharacterStatDefinition definition)
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

        public PlayableCharacterState GetDefaultExplorationState(string characterName)
        {
            if (TryGetDefinition(characterName, out var definition))
            {
                return definition.DefaultExplorationState;
            }

            return DefaultExplorationState;
        }
    }
}
