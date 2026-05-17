using UnityEngine;
namespace Core.Exploration.Items.Currencies
{
    [CreateAssetMenu(menuName = "Exploration/Items/Currency/Epic Anomalous Artifact", fileName = "EpicAnomalousArtifact")]
    public sealed class EpicAnomalousArtifact : ScriptableObject, IStorageable
    {
        public StorageMode storageMode => StorageMode.Stackable;
    }
}