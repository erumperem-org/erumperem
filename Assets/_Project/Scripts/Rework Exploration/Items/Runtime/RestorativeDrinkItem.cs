using Services.DebugUtilities;
using UnityEngine;
using Core.Exploration.Items;

namespace Core.CharacterStats
{
    /// <summary>
    /// Restorative Drink: heals EVERY character in the PlayableCharacterController
    /// roster by a flat amount, through each character's HealthBar (ApplyHeal),
    /// so the bar and its model update together.
    ///
    /// Not bound to a character and needs no target, so it does not implement
    /// ICharacterTargetedItem: the item-use flow goes straight to confirmation.
    ///
    /// The controller lives in the scene, which an asset can't reference, so it
    /// is looked up when the item is used. Outside the exploration scene
    /// (no controller found) the item logs an error and heals nobody.
    ///
    /// Create via: Assets → Create → Character Stats → Restorative Drink
    /// </summary>
    [CreateAssetMenu(menuName = "Character Stats/Restorative Drink", fileName = "RestorativeDrink")]
    public sealed class RestorativeDrinkItem : ItemDefinition
    {
        [Tooltip("Flat health restored to each character, in the same unit as the health bar (HP). " +
                 "Heals are clamped by the bar's max. 0 = no effect (balance is not invented here).")]
        [SerializeField, Min(0f)] private float _healAmount = 0f;

        public float HealAmount => _healAmount;

        public override void ExecuteItemEffect()
        {
            if (_healAmount <= 0f)
            {
                Log(LogLevel.Warning, $"'{StorageableId}' has Heal Amount 0 — nothing healed.");
                return;
            }

            var controller = FindController();
            if (controller == null)
            {
                Log(LogLevel.Error, $"'{StorageableId}': no PlayableCharacterController in the scene — nobody healed.");
                return;
            }

            int healed = 0;

            foreach (var character in controller.Roster)
            {
                var bar = character != null ? character.HealthBar : null;
                if (bar == null || bar.Model == null) continue; // bar not initialized yet

                bar.ApplyHeal(_healAmount);
                healed++;
            }

            Log(LogLevel.Debug, $"'{StorageableId}' healed {healed} character(s) by {_healAmount}.");
        }

        private static PlayableCharacterController FindController()
        {
#if UNITY_2023_1_OR_NEWER
            return FindFirstObjectByType<PlayableCharacterController>();
#else
            return FindObjectOfType<PlayableCharacterController>();
#endif
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[RestorativeDrinkItem:{name}] {msg}", LogCategory.Inventory);
    }
}