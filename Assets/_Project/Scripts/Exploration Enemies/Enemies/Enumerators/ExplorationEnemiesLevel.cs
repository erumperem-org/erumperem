using UnityEngine;

namespace Core.Exploration.Enemies
{
    // Represents the difficulty tier of an enemy in exploration mode.
    // This will be used in the future by other systems (e.g., combat scene builder)
    // to determine scaling factors such as stats, abilities, rewards, and encounter setup.
    public enum ExplorationEnemiesLevel
    {
        //Weak enemies: low stats, simple behavior, early-game encounters
        Low,

        //Medium enemies: balanced stats and mechanics, mid-game encounters
        Medium,

        //Strong enemies: high stats, complex mechanics, late-game or elite encounters
        High
    }
}

