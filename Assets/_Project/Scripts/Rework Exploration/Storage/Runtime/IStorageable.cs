namespace Core.Storage
{
    /// <summary>
    /// Contrato base para qualquer entidade armazenável (item ou moeda).
    /// StorageableId é o identificador único usado por registries para
    /// resolver a entidade em runtime (ver ItemRegistry, CoinRegistry).
    /// </summary>
    public interface InterfaceStorageable
    {
        IStorageStrategy StorageStrategy { get; }
        string StorageableId { get; }
        string Description { get; }
    }
}
