using UnityEngine;
using Core.Exploration.Items;

namespace Core.Exploration.Items.Testing
{
    /// <summary>Second test-only concrete item, so migration tests have more than one distinct item type to move.</summary>
    [CreateAssetMenu(menuName = "Testing/Items/Test Gem", fileName = "TestItem_Gem")]
    public sealed class TestGemItem : ItemDefinition
    {
        public override void ExecuteItemEffect()
        {
            // No-op — this item exists only to exercise storage/migration logic.
        }
    }
}