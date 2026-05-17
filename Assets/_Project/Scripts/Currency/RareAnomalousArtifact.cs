using Core.Exploration.Items;
using UnityEngine;

namespace Core.Exploration.Items.Currencies
{
    [CreateAssetMenu(menuName = "Exploration/Items/Currency/Rare Anomalous Artifact", fileName = "RareAnomalousArtifact")]
    public sealed class RareAnomalousArtifact : ScriptableObject, IStorageable
    {
        public StorageMode storageMode => StorageMode.Stackable;
    }
}