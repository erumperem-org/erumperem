using System;
using System.Collections.Generic;
using UnityEngine;

namespace Erumperem.Characters
{
    /// <summary>
    /// Catálogo central de stats por personagem. Substitui valores hardcoded no código.
    /// </summary>
    [CreateAssetMenu(
        menuName = "Erumperem/Characters/Character Stat Catalog",
        fileName = "CharacterStatCatalog")]
    public sealed class CharacterStatCatalog : ScriptableObject
    {
        private const float DefaultExplorationMaxHealth = 100f;
        private const PlayableCharacterState DefaultExplorationState = PlayableCharacterState.Resting;

        [SerializeField] private List<CharacterStatDefinition> definitions = new();

        public IReadOnlyList<CharacterStatDefinition> Definitions => definitions;

        public bool TryGetDefinition(string characterId, out CharacterStatDefinition definition)
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

        public float GetExplorationMaxHealth(string characterName)
        {
            if (TryGetDefinition(characterName, out var definition))
            {
                return definition.ExplorationMaxHealth;
            }

            return DefaultExplorationMaxHealth;
        }

        public float GetDefaultStartingHealth(string characterName)
        {
            if (TryGetDefinition(characterName, out var definition))
            {
                return definition.ResolveStartingHealth();
            }

            return DefaultExplorationMaxHealth;
        }
    }
}
