namespace Core.Shop
{
    /// <summary>
    /// Parameterless contract for the class (configurable via inspector)
    /// that receives the notification of a successful skill point purchase.
    /// </summary>
    public interface ISkillPointGrantable
    {
        void GrantSkillPoint();
    }
}
