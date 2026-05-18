// ============================================================
// NpcEnemyPool.cs
// Namespace : Systems.NPC.Pool
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
        [Tooltip("Prefab do NPC inimigo.")]
        [SerializeField] private GameObject _npcPrefab;

        [Header("Capacidade")]
        [SerializeField, Min(1)] private int _poolSize = 10;

        [Header("Posição de Armazenamento (fora do mapa)")]
        [SerializeField] private Vector3 _storageOrigin = new Vector3(0f, -100f, 0f);
        [SerializeField] private float   _storageSpacing = 3f;

        // ── Estado interno ────────────────────────────────────────────────

        private readonly Stack<NpcEnemy>   _available = new();
        private readonly HashSet<NpcEnemy> _active    = new();

        // ── Propriedades ──────────────────────────────────────────────────

        public int  AvailableCount => _available.Count;
        public int  ActiveCount    => _active.Count;
        public bool HasAvailable   => _available.Count > 0;

        // ── Evento ────────────────────────────────────────────────────────

        /// <summary>
        /// Disparado sempre que um NPC é devolvido à pool.
        /// O Spawner se inscreve aqui para agendar respawns.
        /// </summary>
        public event Action OnNpcReturned;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_npcPrefab == null)
            {
                Debug.LogError("[NpcEnemyPool] Nenhum prefab configurado!", this);
                return;
            }

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

        public void Return(INpcEnemy npc)
        {
            if (npc is not NpcEnemy enemy)
            {
                Debug.LogWarning("[NpcEnemyPool] Tipo não suportado.");
                return;
            }

            if (!_active.Contains(enemy))
            {
                Debug.LogWarning($"[NpcEnemyPool] '{enemy.name}' não está na lista de ativos.", this);
                return;
            }

            _active.Remove(enemy);
            _available.Push(enemy);

            var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            RepositionInStorage(enemy, _available.Count - 1);
            enemy.gameObject.SetActive(false);

            // Notifica o Spawner para que agende o próximo respawn
            OnNpcReturned?.Invoke();
        }

        // ── Inicialização ─────────────────────────────────────────────────

        private void PrewarmPool()
        {
            Vector3 birthPosition = transform.position;

            for (int i = 0; i < _poolSize; i++)
            {
                var go = Instantiate(_npcPrefab, birthPosition, Quaternion.identity, transform);
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

                go.transform.position = CalculateStoragePosition(i);
                go.transform.rotation = Quaternion.identity;
                go.SetActive(false);

                _available.Push(enemy);
            }

            Debug.Log($"[NpcEnemyPool] Pool inicializada com {_available.Count} NPCs.", this);
        }

        // ── Grade de armazenamento ────────────────────────────────────────

        private Vector3 CalculateStoragePosition(int index)
        {
            int col = index % 2;
            int row = index / 2;
            return _storageOrigin + new Vector3(col * _storageSpacing, 0f, row * _storageSpacing);
        }

        private void RepositionInStorage(NpcEnemy enemy, int indexInPool)
        {
            enemy.transform.position = CalculateStoragePosition(indexInPool);
            enemy.transform.rotation = Quaternion.identity;
        }

        // ── Gizmos ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
            for (int i = 0; i < _poolSize; i++)
                Gizmos.DrawWireCube(CalculateStoragePosition(i), Vector3.one * 0.8f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_storageOrigin, 0.3f);
        }
#endif
    }
}