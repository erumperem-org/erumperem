using UnityEngine;

namespace Core.Exploration.Items.Currencies
{
    public enum ArtifactRarity { Rare, Epic, Legendary }

    [CreateAssetMenu(menuName = "Exploration/Items/Currency/Anomalous Artifact", fileName = "AnomalousArtifact")]
    public sealed class AnomalousArtifact : ScriptableObject, IStorageable
    {
        [SerializeField] private ArtifactRarity rarity;

        public ArtifactRarity Rarity => rarity;
        public StorageMode storageMode => StorageMode.Stackable;
    }
}
