using UnityEngine;

namespace Core.Exploration.Items.Currencies
{
    public enum ArtifactRarity { Rare, Epic, Legendary }

    [CreateAssetMenu(menuName = "Exploration/Items/Currency/Anomalous Artifact", fileName = "AnomalousArtifact")]
    public class AnomalousArtifact : ScriptableObject, IStorageable
    {
        [SerializeField] private ArtifactRarity rarity;
        public ArtifactRarity Rarity => rarity;
        public StorageMode storageMode => StorageMode.Stackable;
        [SerializeField] private string _itemId;
        public string ItemId => _itemId;
        public string Description => _description;
        [SerializeField] private string _description;
        public Sprite Sprite;
    }
}
