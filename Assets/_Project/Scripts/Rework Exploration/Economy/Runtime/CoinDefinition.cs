using UnityEngine;
using Core.Storage;

namespace Core.Economy.Currency
{
    /// <summary>
    /// Concrete ScriptableObject for any currency type in the game.
    /// Unlike ItemDefinition, this is not abstract: coins have no use
    /// effect, so there is no polymorphic behaviour to override.
    ///
    /// Create via: Assets → Create → Economy → Coin Definition
    /// </summary>
    [CreateAssetMenu(menuName = "Economy/Coin Definition", fileName = "Coin_")]
    public sealed class CoinDefinition : ScriptableObject, ICoin
    {
        [Header("Identity")]
        [SerializeField] private string _storageableId;
        [SerializeField] private string _description;
        [SerializeField] private Sprite _sprite;

        [Header("Storage")]
        [SerializeReference] private IStorageStrategy _storageStrategy = new UnlimitedStorageStrategy();

        public string StorageableId => _storageableId;
        public string Description => _description;
        public Sprite Sprite => _sprite;
        public IStorageStrategy StorageStrategy => _storageStrategy;
    }
}
