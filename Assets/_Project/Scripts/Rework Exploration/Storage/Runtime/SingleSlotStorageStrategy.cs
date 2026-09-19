using System;

namespace Core.Storage
{
    /// <summary>Nunca empilha — cada unidade ocupa seu próprio slot — mas várias instâncias podem coexistir.</summary>
    [Serializable]
    public sealed class SingleSlotStorageStrategy : IStorageStrategy
    {
        public bool CanShareSlot => false;
        public int? MaxPerSlot => 1;
        public int? MaxTotalInstances => null;
    }
}
