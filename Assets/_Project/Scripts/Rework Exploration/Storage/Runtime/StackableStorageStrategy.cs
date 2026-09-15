using System;
using UnityEngine;

namespace Core.Storage
{
    /// <summary>Múltiplas unidades compartilham o mesmo slot, até um teto configurável.</summary>
    [Serializable]
    public sealed class StackableStorageStrategy : IStorageStrategy
    {
        [SerializeField] private int _maxPerSlot = 99;

        public bool CanShareSlot => true;
        public int? MaxPerSlot => _maxPerSlot;
        public int? MaxTotalInstances => null;
    }
}
