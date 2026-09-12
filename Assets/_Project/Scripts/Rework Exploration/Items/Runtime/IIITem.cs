using UnityEngine;
using Core.Storage;

namespace Core.Exploration.Items
{
    /// <summary>
    /// Temporary name: a legacy "IItem" interface still exists elsewhere in
    /// the project and is scheduled for future removal. Once that happens,
    /// this contract should be renamed to the correct name (IItem).
    /// </summary>
    public interface IIITem : InterfaceStorageable
    {
        Sprite Sprite { get; }

        /// <summary>Human-readable name for UI (item panels, tooltips, etc.). Distinct from StorageableId, which is an opaque identifier.</summary>
        string DisplayName { get; }

        /// <summary>View-only. Does not affect system logic.</summary>
        ItemRarity Rarity { get; }

        void ExecuteItemEffect();
    }
}