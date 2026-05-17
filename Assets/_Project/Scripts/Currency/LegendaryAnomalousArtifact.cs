using UnityEngine;
namespace Core.Exploration.Items.Currencies
{
    [CreateAssetMenu(menuName = "Exploration/Items/Currency/Legendary Anomalous Artifact", fileName = "LegendaryAnomalousArtifact")]
    public sealed class LegendaryAnomalousArtifact : ScriptableObject, IStorageable
    {
        public StorageMode storageMode => StorageMode.Stackable;
    }
}