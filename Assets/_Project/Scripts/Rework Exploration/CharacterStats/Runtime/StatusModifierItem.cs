using System.Collections.Generic;
using Erumperem.Characters;
using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;

namespace Core.CharacterStats
{
    /// <summary>
    /// Item that permanently modifies a character's stats (saved as deltas
    /// over the base stats of AllyCharacterStatDefinition). It is not bound to
    /// any character, so it implements <see cref="ICharacterTargetedItem"/>:
    /// the caller must tell it WHICH character to act on.
    ///
    /// Authoring: each entry holds a stat, Absolute/Relative and an intensity
    /// symbol (+, ++, --, ...). The numbers come from the shared
    /// <see cref="IntensityLibrary"/> — assign the SAME library asset to every item.
    ///
    /// Create via: Assets → Create → Character Stats → Status Modifier Item
    /// </summary>
    [CreateAssetMenu(menuName = "Character Stats/Status Modifier Item", fileName = "StatusItem_")]
    public sealed class StatusModifierItem : ItemDefinition, ICharacterTargetedItem
    {
        [Tooltip("Shared asset mapping intensity symbols to numbers. Use the same one in every item.")]
        [SerializeField] private IntensityLibrary _intensityLibrary;

        [Tooltip("One entry per affected stat. Individual-status items configure exactly one; thematic items configure two or more.")]
        [SerializeField] private List<StatModifierEntry> _modifiers = new();

        public IReadOnlyList<StatModifierEntry> Modifiers => _modifiers;

        public ItemApplicability CanApplyTo(AllyCharacterStatDefinition target)
            => Evaluate(target, out _);

        public void ExecuteItemEffect(AllyCharacterStatDefinition target)
        {
            var check = Evaluate(target, out var resolved);
            if (!check.CanApply)
            {
                Log(LogLevel.Warning, $"'{StorageableId}' refused: {check.Reason}");
                return;
            }

            CharacterStatsRepository.ApplyModifiers(target, resolved);
        }

        /// <summary>
        /// Part of the IIITem contract, which carries no context. A status item
        /// is meaningless without a target, so this does nothing but warn.
        /// Use <see cref="ExecuteItemEffect(AllyCharacterStatDefinition)"/>.
        /// </summary>
        public override void ExecuteItemEffect()
        {
            Log(LogLevel.Warning,
                $"'{StorageableId}' needs a target character. Call ExecuteItemEffect(AllyCharacterStatDefinition) via ICharacterTargetedItem.");
        }

        private ItemApplicability Evaluate(AllyCharacterStatDefinition target, out List<ResolvedStatModifier> resolved)
        {
            resolved = null;

            if (target == null)
                return ItemApplicability.Denied("No character selected.");

            if (_intensityLibrary == null)
                return ItemApplicability.Denied("Item has no IntensityLibrary assigned.");

            resolved = new List<ResolvedStatModifier>(_modifiers.Count);
            foreach (var entry in _modifiers)
                resolved.Add(entry.Resolve(_intensityLibrary));

            var saved = CharacterStatsRepository.GetStatsForCharacter(target.CharacterId);
            return StatApplicabilityRule.Evaluate(target, saved, resolved);
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[StatusModifierItem:{name}] {msg}", LogCategory.Inventory);
    }
}
