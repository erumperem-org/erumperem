// ============================================================
// NpcEnemyPool.cs
// Namespace : Systems.NPC.Pool
// ============================================================
// Responsabilidade única: gerenciar disponibilidade dos NPCs
// (Get / Return / PreWarm).
//
// Mudanças em relação à versão original:
//   • Posicionamento físico delegado a IPoolStorage (GridPoolStorage)
//   • Return() aceita NpcEnemy diretamente — o cast de INpcEnemy para
//     NpcEnemy foi movido para NpcEnemyBuilder, que é quem conhece o
//     tipo concreto. A pool não precisa depender da interface para
//     depois fazer cast internamente.
// ============================================================

using System;
using System.Collections.Generic;
using Systems.NPC.Enemy;
using Systems.NPC.Enemy.Contracts;
using UnityEngine;

namespace Systems.NPC.Pool
{
    public sealed class NpcEnemyPool : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject _npcPrefab;

        [Header("Capacidade")]
        [SerializeField, Min(1)] private int _poolSize = 10;

        [Header("Armazenamento (fora do mapa)")]
        [SerializeField] private Vector3 _storageOrigin  = new Vector3(0f, -100f, 0f);
        [SerializeField] private float   _storageSpacing = 3f;

        // ── Estado interno ────────────────────────────────────────────────

        private readonly Stack<NpcEnemy>   _available = new();
        private readonly HashSet<NpcEnemy> _active    = new();
        private IPoolStorage               _storage;

        // ── Propriedades ──────────────────────────────────────────────────

        public int  AvailableCount => _available.Count;
        public int  ActiveCount    => _active.Count;
        public bool HasAvailable   => _available.Count > 0;

        public bool HasActiveEnemyNear(Vector3 position, float radius)
        {
            float radiusSquared = radius * radius;
            foreach (NpcEnemy activeEnemy in _active)
            {
                if (activeEnemy == null) continue;
                if ((activeEnemy.transform.position - position).sqrMagnitude <= radiusSquared)
                    return true;
            }

            return false;
        }

        // ── Evento ────────────────────────────────────────────────────────

        public event Action OnNpcReturned;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_npcPrefab == null)
            {
                Debug.LogError("[NpcEnemyPool] Nenhum prefab configurado!", this);
                return;
            }

            _storage = new GridPoolStorage(_storageOrigin, _storageSpacing);
            PrewarmPool();
        }

        // ── API pública ───────────────────────────────────────────────────

        public NpcEnemy Get()
        {
            if (!HasAvailable)
            {
                Debug.LogWarning("[NpcEnemyPool] Pool esgotada.", this);
                return null;
            }

            var npc = _available.Pop();
            _active.Add(npc);
            npc.gameObject.SetActive(true);
            return npc;
        }

        /// <summary>
        /// Recebe NpcEnemy diretamente — sem cast de interface para concreto.
        /// O Builder, que conhece o tipo concreto, é quem chama este método.
        /// </summary>
        public void Return(NpcEnemy enemy)
        {
            if (enemy == null) return;

            if (!_active.Contains(enemy))
            {
                Debug.LogWarning($"[NpcEnemyPool] '{enemy.name}' não está na lista de ativos.", this);
                return;
            }

            _active.Remove(enemy);
            _available.Push(enemy);

            var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            _storage.StoreAt(enemy, _available.Count - 1);
            enemy.gameObject.SetActive(false);

            OnNpcReturned?.Invoke();
        }

        // ── PreWarm ───────────────────────────────────────────────────────

        private void PrewarmPool()
        {
            for (int i = 0; i < _poolSize; i++)
            {
                var go = Instantiate(_npcPrefab, _storage.PositionFor(i), Quaternion.identity, transform);
                go.name = $"NpcEnemy_{i:D2}";

                var enemy = go.GetComponent<NpcEnemy>();
                if (enemy == null)
                {
                    Debug.LogError($"[NpcEnemyPool] '{go.name}' sem NpcEnemy.", this);
                    Destroy(go);
                    continue;
                }

                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null) agent.enabled = false;

                go.SetActive(false);
                _available.Push(enemy);
            }

            Debug.Log($"[NpcEnemyPool] Pool inicializada com {_available.Count} NPCs.", this);
        }

        // ── Gizmos ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            var storage = new GridPoolStorage(_storageOrigin, _storageSpacing);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
            for (int i = 0; i < _poolSize; i++)
                Gizmos.DrawWireCube(storage.PositionFor(i), Vector3.one * 0.8f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_storageOrigin, 0.3f);
        }
#endif
    }
}
