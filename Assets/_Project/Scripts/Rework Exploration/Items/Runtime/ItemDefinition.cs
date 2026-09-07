using UnityEngine;
using Core.Storage;

namespace Core.Exploration.Items
{
    /// <summary>
    /// Abstract ScriptableObject base for any concrete game item.
    /// Implements IIITem and centralizes the common fields (id, display
    /// name, description, sprite, rarity, storage strategy). Concrete items
    /// (potions, weapons, skill-tree items, etc.) should inherit from this
    /// and override ExecuteItemEffect() — the execution context is
    /// intentionally empty (parameterless), by design.
    ///
    /// Create concrete types via [CreateAssetMenu] on the subclass.
    /// </summary>
    public abstract class ItemDefinition : ScriptableObject, IIITem
    {
        [Header("Identity")]
        [SerializeField] private string _storageableId;
        [SerializeField] private string _displayName;
        [SerializeField] private string _description;
        [SerializeField] private Sprite _sprite;

        [Tooltip("View-only. Does not affect system logic.")]
        [SerializeField] private ItemRarity _rarity = ItemRarity.Common;

        [Header("Storage")]
        [SerializeReference] private IStorageStrategy _storageStrategy = new StackableStorageStrategy();

        public string StorageableId => _storageableId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Sprite => _sprite;
        public ItemRarity Rarity => _rarity;
        public IStorageStrategy StorageStrategy => _storageStrategy;

        public abstract void ExecuteItemEffect();
    }
}