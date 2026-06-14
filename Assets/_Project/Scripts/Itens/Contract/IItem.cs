using UnityEngine;
namespace Core.Exploration.Items
{
    public interface IItem : IStorageable
    {
        public Sprite Sprite { get; }
        void ExecuteItemEffect();
    }
}
