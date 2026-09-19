using System;

namespace Core.Storage
{
    /// <summary>Apenas uma instância pode existir no container inteiro.</summary>
    [Serializable]
    public sealed class UniqueStorageStrategy : IStorageStrategy
    {
        public bool CanShareSlot => false;
        public int? MaxPerSlot => 1;
        public int? MaxTotalInstances => 1;
    }
}
