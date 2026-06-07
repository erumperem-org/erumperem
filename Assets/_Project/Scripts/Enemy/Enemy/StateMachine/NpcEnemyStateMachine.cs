// ============================================================
// NpcEnemyStateMachine.cs
// Namespace : Systems.NPC.Enemy.StateMachine
// ============================================================
// Responsabilidade única: gerenciar transições de estado
// e disparar eventos de entrada/saída de cada estado.
//
// Não sabe nada de coroutines, navegação ou detecção.
// ============================================================

using System;
using Systems.NPC.Enemy.Contracts;

namespace Systems.NPC.Enemy.StateMachine
{
    public sealed class NpcEnemyStateMachine
    {
        public NpcEnemyState Current { get; private set; } = NpcEnemyState.Idle;

        public event Action OnEnterWander;
        public event Action<UnityEngine.Transform> OnEnterChase;
        public event Action OnEnterReturnToPool;

        public void ToWander()
        {
            if (Current == NpcEnemyState.ReturningToPool) return;
            Current = NpcEnemyState.Wander;
            OnEnterWander?.Invoke();
        }

        public void ToChase(UnityEngine.Transform target)
        {
            if (Current == NpcEnemyState.ReturningToPool) return;
            if (target == null) return;
            Current = NpcEnemyState.Chase;
            OnEnterChase?.Invoke(target);
        }

        public void ToReturnToPool()
        {
            if (Current == NpcEnemyState.ReturningToPool) return;
            Current = NpcEnemyState.ReturningToPool;
            OnEnterReturnToPool?.Invoke();
        }

        public bool Is(NpcEnemyState state) => Current == state;
    }
}
