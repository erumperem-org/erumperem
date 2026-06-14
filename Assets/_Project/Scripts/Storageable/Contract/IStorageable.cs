public interface IStorageable
{
   public StorageMode storageMode { get; }
   string ItemId { get; }
   string Description { get; }
}