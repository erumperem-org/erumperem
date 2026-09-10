using UnityEngine;

namespace Core.Exploration.Items
{
    /// <summary>
    /// Raridade de um item, usada exclusivamente para apresentação
    /// (cor de borda, ícone, ordenação visual etc.). Não influencia
    /// IStorageStrategy nem qualquer regra de sistema. A ordem de
    /// declaração importa: comparações por ordinal (ex: melhor item
    /// de um baú) dependem desta sequência.
    /// </summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }
}

namespace Core.Exploration.Items
{
    /// <summary>
    /// Global, project-wide mapping of ItemRarity → display color. Any
    /// system that needs to visually represent rarity (chest view, item
    /// tooltips, inventory slot borders, etc.) should reference the same
    /// asset instance, instead of keeping its own local color array.
    ///
    /// Create via: Assets → Create → Items → Rarity Color Palette
    /// (intended to exist as a single shared asset in the project).
    /// </summary>
    [CreateAssetMenu(menuName = "Items/Rarity Color Palette", fileName = "RarityColorPalette")]
    public sealed class RarityColorPalette : ScriptableObject
    {
        [SerializeField] private Color _common = Color.white;
        [SerializeField] private Color _uncommon = Color.green;
        [SerializeField] private Color _rare = Color.blue;
        [SerializeField] private Color _epic = new(0.6f, 0.2f, 0.8f); // purple
        [SerializeField] private Color _legendary = new(1f, 0.65f, 0f); // orange

        public Color GetColor(ItemRarity rarity) => rarity switch
        {
            ItemRarity.Common => _common,
            ItemRarity.Uncommon => _uncommon,
            ItemRarity.Rare => _rare,
            ItemRarity.Epic => _epic,
            ItemRarity.Legendary => _legendary,
            _ => Color.white
        };
    }
}