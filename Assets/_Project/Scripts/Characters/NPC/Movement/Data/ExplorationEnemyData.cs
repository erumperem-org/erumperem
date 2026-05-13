using System;
using Core.Exploration.Character.NPC;
using UnityEngine;

namespace Core.Exploration.Character.NPC.Enemy
{
    [Serializable]
    public class ExplorationEnemyData
    {
        [Tooltip("Nível de força/dificuldade do inimigo.")]
        public ExplorationEnemyLevels enemyLevel;
    }
}