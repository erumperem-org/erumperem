using System;
using UnityEngine;
using Core.Storage;

namespace Core.Rewards
{
    /// <summary>
    /// Entry in a LootTable. Evaluated independently of the others: each
    /// entry has its own chance to be granted, without affecting the rest.
    /// </summary>
    [Serializable]
    public sealed class LootEntry
    {
        [Tooltip("Must implement InterfaceStorageable (item or coin).")]
        [SerializeField] private ScriptableObject _storageableAsset;

        [Tooltip("Independent chance of this entry being granted, from 0 to 100.")]
        [Range(0f, 100f)]
        [SerializeField] private float _chancePercent = 10f;

        [Tooltip("Minimum quantity (inclusive) if this entry is granted.")]
        [SerializeField] private int _minQuantity = 1;

        [Tooltip("Maximum quantity (inclusive) if this entry is granted.")]
        [SerializeField] private int _maxQuantity = 1;

        public InterfaceStorageable Storageable => _storageableAsset as InterfaceStorageable;
        public float ChancePercent => _chancePercent;
        public int MinQuantity => _minQuantity;
        public int MaxQuantity => _maxQuantity;

        public bool IsValid => Storageable != null && _maxQuantity >= _minQuantity && _minQuantity > 0;
    }
}
