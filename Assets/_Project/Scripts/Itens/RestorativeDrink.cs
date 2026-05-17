using Core.Exploration.Items;
using UnityEngine;

namespace Core.Exploration.Items.Usables
{
    [CreateAssetMenu(menuName = "Exploration/Items/Usable/Restorative Drink", fileName = "RestorativeDrink")]
    public sealed class RestorativeDrink : ScriptableObject, IItem
    {
        public StorageMode storageMode => StorageMode.Stackable;

        public void ExecuteItemEffect()
        {
            throw new System.NotImplementedException();
        }
    }
}