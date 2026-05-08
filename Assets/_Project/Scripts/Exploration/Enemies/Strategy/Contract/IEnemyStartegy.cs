using System.Threading.Tasks;
using UnityEngine;

// ─────────────────────────────────────────────
//  Interfaces
// ─────────────────────────────────────────────

public interface IEnemyStartegy
{
    Task ExecuteBehavior(IEnemyStartegyContext context);
}

public interface IReverseableEnemyStartegy : IEnemyStartegy
{
    Task UnexecuteBehavior(IEnemyStartegyContext context);
    void CancelImmediate();
}

public interface IEnemyStartegyContext { }

// ─────────────────────────────────────────────
//  Context classes
// ─────────────────────────────────────────────

public class PatrolBehaviorContext : IEnemyStartegyContext
{
    public ExplorationEnemyController enemy;
    public Transform target;
    public float perceptionRadius;

    public PatrolBehaviorContext(
        ExplorationEnemyController enemy,
        Transform target,
        float perceptionRadius)
    {
        this.enemy            = enemy;
        this.target           = target;
        this.perceptionRadius = perceptionRadius;
    }
}

public class PursuingBehaviorContext : IEnemyStartegyContext
{
    public ExplorationEnemyController enemy;
    public Transform target;
    public float perceptionRadius;

    public PursuingBehaviorContext(
        ExplorationEnemyController enemy,
        Transform target,
        float perceptionRadius)
    {
        this.enemy            = enemy;
        this.target           = target;
        this.perceptionRadius = perceptionRadius;
    }
}

public class StalkingBehaviorContext : IEnemyStartegyContext
{
    public ExplorationEnemyController enemy;
    public Transform target;
    public float stalkingDistance;

    public StalkingBehaviorContext(
        ExplorationEnemyController enemy,
        Transform target,
        float stalkingDistance)
    {
        this.enemy           = enemy;
        this.target          = target;
        this.stalkingDistance = stalkingDistance;
    }
}

public class OnPoolBehaviorContext : IEnemyStartegyContext
{
    public ExplorationEnemyController enemy;
    public Vector3 newPosition;
    public Transform parent;
    public OnPoolBehaviorContext(
        ExplorationEnemyController enemy,
        Vector3 newPosition,
        Transform parent)
    {
        this.enemy       = enemy;
        this.newPosition = newPosition;
        this.parent = parent;
    }
}
