using UnityEngine;
using Services.DebugUtilities;
using Core.Exploration.Items;

namespace Core.CharacterStats.Testing
{
    /// <summary>
    /// Editor-only test harness: calls ExecuteItemEffect() on a reference
    /// item via an inspector button. Works for any IIITem — including
    /// StatusModifierItem and SkillTreeResetItem — since both are exercised
    /// through the same interface method.
    /// </summary>
    public sealed class ItemEffectTestbed : MonoBehaviour
    {
        [Tooltip("Must implement IIITem.")]
        [SerializeField] private ScriptableObject _itemAsset;

        private IIITem Item => _itemAsset as IIITem;

        public void ExecuteEffect()
        {
            if (Item == null)
            {
                Log(LogLevel.Error, "Item asset not assigned or does not implement IIITem.");
                return;
            }

            Item.ExecuteItemEffect();
            Log(LogLevel.Debug, $"Executed effect for '{Item.StorageableId}'.");
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[ItemEffectTestbed:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}