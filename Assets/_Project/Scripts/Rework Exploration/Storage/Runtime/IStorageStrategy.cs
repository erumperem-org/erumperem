namespace Core.Storage
{
    /// <summary>
    /// Política de armazenamento de uma entidade — como múltiplas unidades/
    /// instâncias dela se comportam ao entrar em um container (inventário,
    /// baú, etc). Implementações concretas encapsulam a regra; o container
    /// consumidor nunca precisa de switch sobre um enum de modos.
    /// </summary>
    public interface IStorageStrategy
    {
        /// <summary>Pode coexistir com outras unidades de si mesma no mesmo slot?</summary>
        bool CanShareSlot { get; }

        /// <summary>Quantidade máxima por slot. Null = sem limite.</summary>
        int? MaxPerSlot { get; }

        /// <summary>Quantidade máxima total no container. Null = sem limite; 1 = item único.</summary>
        int? MaxTotalInstances { get; }
    }
}
