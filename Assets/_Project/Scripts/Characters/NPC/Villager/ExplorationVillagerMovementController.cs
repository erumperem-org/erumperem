using System.Collections.Generic;
using System.Threading.Tasks;
using Core.Exploration.Character.Movement;
using Services.Navigation;
using Unity.VisualScripting;
using UnityEngine;

namespace Core.Exploration.Character.NPC.Companion
{
    public class ExplorationVillagerMovementController : ExplorationNpcMovementController
    {
        public NavMeshService navMeshService;
        public List<GameObject> wayPoints;

        void OnEnable()
        {
            _ = ExplorationNpcMovementController.SetNpcMovementStartegy(this, new WanderBehavior(), new WanderBehaviorContext(this.data, wayPoints.ConvertAll(go => go.transform.position), this.transform, this.navMeshService, true, false));
        }

        void OnDisable()
        {
           _ = ExplorationNpcMovementController.SetNpcMovementStartegy(this, new OnPoolBehavior(), new OnPoolBehaviorContext(this.data, new Vector3(0,-1000,0), null, this.transform));
        }
    }
}
