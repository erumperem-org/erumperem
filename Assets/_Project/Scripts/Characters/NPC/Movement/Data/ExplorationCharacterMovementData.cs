using System;
using Core.Exploration.Character.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Character
{
    [Serializable]
    public class ExplorationCharacterMovementData
    {
        public string EnemyStartegyExposed;
        public ICharacterMovementStartegy ActiveStrategy;
        public ICharacterMovementStartegyContext CurrentContext;
        public NavMeshAgent Agent;
    }
}
