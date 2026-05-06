namespace Core.Exploration.Enemies
{
    public interface IExplorationEnemyStrategy
    {
        ExplorationEnemyStates state { get; }
        void EnterBehavior();
        void ExitBehavior();
        void UpdateExplorationState(ref ExplorationEnemyStates toUpdate) => toUpdate = state;
    }
}
