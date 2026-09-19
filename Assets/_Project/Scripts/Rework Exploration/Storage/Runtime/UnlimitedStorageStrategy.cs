using System;

namespace Core.Storage
{
    /// <summary>Sem limite de quantidade ou slots. Uso típico: sistemas simplificados.</summary>
    [Serializable]
    public sealed class UnlimitedStorageStrategy : IStorageStrategy
    {
        public bool CanShareSlot => true;
        public int? MaxPerSlot => null;
        public int? MaxTotalInstances => null;
    }
}
