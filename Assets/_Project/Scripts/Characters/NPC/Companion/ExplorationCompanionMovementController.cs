using System.Threading.Tasks;
using Core.Exploration.Character.Movement;
using Services.Navigation;
using UnityEngine;

namespace Core.Exploration.Character.NPC.Companion
{
    public class ExplorationCompanionMovementController : ExplorationNpcMovementController
    {
        public float stalkingDistance;
        public Transform target;
        public NavMeshService navMeshService;
        public Vector3 disablePosition;

        void OnEnable()
        {
           _ = ExplorationNpcMovementController.SetNpcMovementStartegy(this, new StalkingBehavior(), new StalkingBehaviorContext(this.data, stalkingDistance, target, this.transform, navMeshService));
        }

        void OnDisable()
        {
           _ = ExplorationNpcMovementController.SetNpcMovementStartegy(this, new OnPoolBehavior(), new OnPoolBehaviorContext(this.data, disablePosition, null, this.transform));
        }
    }
}
