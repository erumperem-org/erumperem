namespace Core.Exploration.Items
{
    public interface IItem : IStorageable
    {
        void ExecuteItemEffect();
    }
}
