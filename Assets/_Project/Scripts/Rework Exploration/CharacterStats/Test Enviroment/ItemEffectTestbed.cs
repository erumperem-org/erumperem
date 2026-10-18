using Erumperem.Characters;
using UnityEngine;
using Services.DebugUtilities;
using Core.Exploration.Items;

namespace Core.CharacterStats.Testing
{
    /// <summary>
    /// Editor-only test harness: executes an item's effect via an inspector
    /// button. Items implementing ICharacterTargetedItem are run against the
    /// assigned target (after the applicability check); all others go through
    /// the plain IIITem.ExecuteItemEffect().
    /// </summary>
    public sealed class ItemEffectTestbed : MonoBehaviour
    {
        [Tooltip("Must implement IIITem.")]
        [SerializeField] private ScriptableObject _itemAsset;

        [Tooltip("Only used by items that implement ICharacterTargetedItem.")]
        [SerializeField] private AllyCharacterStatDefinition _target;

        private IIITem Item => _itemAsset as IIITem;

        public void ExecuteEffect()
        {
            if (Item == null)
            {
                Log(LogLevel.Error, "Item asset not assigned or does not implement IIITem.");
                return;
            }

            if (Item is ICharacterTargetedItem targeted)
            {
                if (_target == null)
                {
                    Log(LogLevel.Error, $"'{Item.StorageableId}' needs a target character — assign one.");
                    return;
                }

                var check = targeted.CanApplyTo(_target);
                if (!check.CanApply)
                {
                    Log(LogLevel.Warning, $"'{Item.StorageableId}' cannot be applied: {check.Reason}");
                    return;
                }

                targeted.ExecuteItemEffect(_target);
            }
            else
            {
                Item.ExecuteItemEffect();
            }

            Log(LogLevel.Debug, $"Executed effect for '{Item.StorageableId}'.");
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[ItemEffectTestbed:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}
