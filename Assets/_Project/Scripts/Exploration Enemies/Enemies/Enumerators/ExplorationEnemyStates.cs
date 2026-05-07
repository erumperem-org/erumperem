using System;
using UnityEngine;

namespace Core.Exploration.Enemies
{
    [Serializable]
    // Defines the possible states an enemy can have during exploration gameplay (Used Only to expose the current strategy on inspector)
    public enum ExplorationEnemyStates
    {
        // Enemy is roaming randomly or following a patrol path
        Wandering,

        // Enemy has detected a target (e.g., player) and is actively pursuing
        Chasing,

        // Enemy is on pool
        OnPool,

        // Enemy is being destroyed (cleanup logic)
        OnDestroy,

        // Enemy has just been created or spawned into the game world
        OnCreate,

        Null
    }
}

