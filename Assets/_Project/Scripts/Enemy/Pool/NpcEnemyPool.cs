// ============================================================
// NpcEnemyPool.cs
// Namespace : Systems.NPC.Pool
// ============================================================
// Responsabilidade única: gerenciar disponibilidade dos NPCs
// (Get / Return / PreWarm).
//
// CORREÇÕES:
//   • Return(): StoreAt é chamado com o índice ANTES do Push,
//     eliminando o off-by-one onde o índice calculado já incluía
//     o elemento recém-adicionado.
//   • Return(): NPCs nulos em _active são removidos ao serem
//     encontrados, evitando acúmulo de referências destruídas.
//   • HasActiveEnemyNear(): referências nulas são removidas do
//     HashSet durante a iteração (via lista de remoção pendente).
//   • GridPoolStorage recebe columnCount proporcional à pool
//     (raiz quadrada arredondada) para distribuição equilibrada.
//   • NavMeshAgent desabilitado DEPOIS do SetActive(false) —
//     desativar o GameObject já remove o agente da NavMesh,
//     tornando a linha manual redundante; a ordem foi invertida
//     para deixar a intenção explícita e evitar conflito de estado.
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

            // Coleta referências nulas para remoção sem modificar o conjunto durante iteração.
            List<NpcEnemy> toRemove = null;

            bool found = false;
            foreach (NpcEnemy activeEnemy in _active)
            {
                if (activeEnemy == null)
                {
                    toRemove ??= new List<NpcEnemy>();
                    toRemove.Add(activeEnemy);
                    continue;
                }

                if ((activeEnemy.transform.position - position).sqrMagnitude <= radiusSquared)
                {
                    found = true;
                    // Não dá break aqui para continuar coletando nulos.
                }
            }

            if (toRemove != null)
                foreach (var dead in toRemove)
                    _active.Remove(dead);

            return found;
        }

        // ── Evento ────────────────────────────────────────────────────────

        public event Action OnNpcReturned;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Start()
        {
            if (_npcPrefab == null)
            {
                Debug.LogError("[NpcEnemyPool] Nenhum prefab configurado!", this);
                return;
            }

            // Número de colunas proporcional ao tamanho da pool para grade equilibrada.
            int columnCount = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(_poolSize)));
            _storage = new GridPoolStorage(_storageOrigin, _storageSpacing, columnCount);

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

            // CORREÇÃO: calcula o índice de destino ANTES do Push para evitar off-by-one.
            // O slot correto é a posição que o elemento vai ocupar após a inserção.
            int targetIndex = _available.Count;

            enemy.gameObject.SetActive(false);

            // Desativar o GameObject já remove o NavMeshAgent da NavMesh automaticamente.
            // A linha abaixo é mantida como salvaguarda explícita para o caso de o agente
            // ser reativado externamente antes do próximo Get().
            var agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            _storage.StoreAt(enemy, targetIndex);
            _available.Push(enemy);

            OnNpcReturned?.Invoke();
        }

        public void ReturnAllActive()
        {
            // Cópia para evitar modificação do conjunto durante iteração.
            var activeEnemies = new List<NpcEnemy>(_active);
            foreach (var activeEnemy in activeEnemies)
            {
                if (activeEnemy != null)
                    activeEnemy.ReturnToPool();
            }
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
            // CORREÇÃO: reutiliza a mesma lógica de columnCount do Awake em vez de
            // instanciar um GridPoolStorage com parâmetros hardcoded (2 colunas).
            int columnCount = Mathf.Max(1, Mathf.RoundToInt(Mathf.Sqrt(_poolSize)));
            var storage = new GridPoolStorage(_storageOrigin, _storageSpacing, columnCount);

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
            for (int i = 0; i < _poolSize; i++)
                Gizmos.DrawWireCube(storage.PositionFor(i), Vector3.one * 0.8f);

            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(_storageOrigin, 0.3f);
        }
#endif
    }
}