using System;
using Core.Exploration.Character.Movement;
using UnityEngine;
using UnityEngine.AI;

namespace Core.Exploration.Character.NPC
{
    [Serializable]
    public class ExplorationNpcMovementData : CharacterData
    {
        public ExplorationCharacterMovementData movementData;
        public float perceptionRadius;
        public float patrolRadius;
    }
}

