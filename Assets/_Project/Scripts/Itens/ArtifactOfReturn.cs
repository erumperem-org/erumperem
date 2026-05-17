using Core.Exploration.Items;
using UnityEngine;

namespace Core.Exploration.Items.Usables
{
    [CreateAssetMenu(menuName = "Exploration/Items/Usable/Artifact of Return", fileName = "ArtifactOfReturn")]
    public sealed class ArtifactOfReturn : ScriptableObject, IItem
    {
        public StorageMode storageMode => StorageMode.Unique;

        public void ExecuteItemEffect()
        {
            throw new System.NotImplementedException();
        }
    }
}