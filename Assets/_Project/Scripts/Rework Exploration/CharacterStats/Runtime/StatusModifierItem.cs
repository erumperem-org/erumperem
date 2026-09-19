using System.Collections.Generic;
using UnityEngine;
using Core.Exploration.Items;

namespace Core.CharacterStats
{
    /// <summary>
    /// Item that permanently modifies a character's base stats, persisted
    /// in the single shared character_stats.json file (via
    /// CharacterStatsRepository). Does not trigger any concrete gameplay
    /// system directly — it only rewrites base stat data on disk, which
    /// other systems are expected to read when computing effective stats.
    ///
    /// Covers both individual-status items (Health Potion, Iron Plate,
    /// Sharp Edge, etc. — one StatModifierEntry) and thematic multi-status
    /// items (Paladino, Vampírico, etc. — multiple entries at once).
    ///
    /// Create via: Assets → Create → Character Stats → Status Modifier Item
    /// </summary>
    [CreateAssetMenu(menuName = "Character Stats/Status Modifier Item", fileName = "StatusItem_")]
    public sealed class StatusModifierItem : ItemDefinition
    {
        [Tooltip("One entry per affected stat. Individual-status items configure exactly one; thematic items configure two or more.")]
        [SerializeField] private List<StatModifierEntry> _modifiers = new();

        public IReadOnlyList<StatModifierEntry> Modifiers => _modifiers;

        public override void ExecuteItemEffect()
        {
            // Character id lookup is a placeholder for now — see
            // CharacterIdentityResolver for where the real lookup goes.
            string characterId = CharacterIdentityResolver.GetCurrentCharacterId();

            CharacterStatsRepository.ApplyModifiers(characterId, _modifiers);
        }
    }
}
