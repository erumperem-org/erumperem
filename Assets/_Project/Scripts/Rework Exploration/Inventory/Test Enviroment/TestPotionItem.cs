using UnityEngine;
using Core.Exploration.Items;

namespace Core.Exploration.Items.Testing
{
    /// <summary>Test-only concrete item. Effect intentionally empty — used to exercise inventory/migration behaviour.</summary>
    [CreateAssetMenu(menuName = "Testing/Items/Test Potion", fileName = "TestItem_Potion")]
    public sealed class TestPotionItem : ItemDefinition
    {
        public override void ExecuteItemEffect()
        {
            // No-op — this item exists only to exercise storage/migration logic.
        }
    }
}