// ============================================================
// NpcEnemySpawner.cs
// Namespace : Systems.NPC.Spawner
// ============================================================
// Ciclo de respawn:
//
//   Start → SpawnBatch (spawn inicial)
//               ↓
//   NPC morre → ReturnToPool → pool.OnNpcReturned
//               ↓
//   Spawner ouve → aguarda _respawnDelay segundos → Build()
//
// O Spawner também mantém o loop periódico original para
// preencher slots que nunca foram usados (ex: pool não cheia).
// ============================================================

using System.Collections;
using Systems.NPC.Builder;
using Systems.NPC.Pool;
using UnityEngine;

namespace Systems.NPC.Spawner
{
    public sealed class NpcEnemySpawner : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Dependências")]
        [SerializeField] private NpcEnemyBuilder _builder;
        [SerializeField] private NpcEnemyPool    _pool;

        [Header("Spawn periódico")]
        [Tooltip("Intervalo entre batches periódicos (preenche slots vazios).")]
        [SerializeField, Min(0.1f)] private float _spawnInterval = 5f;
        [SerializeField, Min(1)]    private int   _spawnBatchSize = 1;
        [SerializeField]            private bool  _autoStart = true;

        [Header("Respawn após retorno à pool")]
        [Tooltip("Segundos de espera depois que um NPC morre antes de respawnar um novo.")]
        [SerializeField, Min(0f)] private float _respawnDelay = 3f;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private Transform   _playerTransform;
        [SerializeField, Min(0f)] private float _playerVisionRadius = 10f;

        // ── Estado interno ────────────────────────────────────────────────

        private Coroutine  _spawnLoopCoroutine;
        private bool       _isRunning;
        private int        _spawnPointIndex;
        private Transform[] _sortedSpawnPoints = new Transform[0];
        private int        _sortedIndex;

        public bool IsRunning => _isRunning;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_builder == null) Debug.LogError("[NpcEnemySpawner] Builder não configurado!", this);
            if (_pool    == null) Debug.LogError("[NpcEnemySpawner] Pool não configurada!",    this);
        }

        private void OnEnable()
        {
            if (_pool != null)
                _pool.OnNpcReturned += HandleNpcReturned;
        }

        private void OnDisable()
        {
            if (_pool != null)
                _pool.OnNpcReturned -= HandleNpcReturned;
        }

        private void Start()
        {
            if (_autoStart)
                StartSpawning();
        }

        private void OnDestroy() => StopSpawning();

        // ── API pública ───────────────────────────────────────────────────

        public void StartSpawning()
        {
            if (_isRunning) return;
            _isRunning = true;
            _spawnLoopCoroutine = StartCoroutine(SpawnLoopCoroutine());
        }

        public void StopSpawning()
        {
            _isRunning = false;
            if (_spawnLoopCoroutine == null) return;
            StopCoroutine(_spawnLoopCoroutine);
            _spawnLoopCoroutine = null;
        }

        public void SpawnBatchNow()  => ExecuteSpawnBatch();
        public void SpawnAt(Vector3 position) => _builder?.BuildAt(position);

        // ── Respawn reativo ───────────────────────────────────────────────

        /// <summary>
        /// Chamado pela pool cada vez que um NPC é devolvido.
        /// Agenda um único respawn após _respawnDelay segundos.
        /// </summary>
        private void HandleNpcReturned()
        {
            StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            if (_respawnDelay > 0f)
                yield return new WaitForSeconds(_respawnDelay);

            // Só respawna se a pool tiver slot disponível
            // (pode ter sido preenchido pelo loop periódico antes do delay acabar)
            if (_pool.HasAvailable)
                SpawnOne();
        }

        // ── Loop periódico (preenche slots nunca usados) ──────────────────

        private IEnumerator SpawnLoopCoroutine()
        {
            ExecuteSpawnBatch();

            while (_isRunning)
            {
                yield return new WaitForSeconds(_spawnInterval);
                if (_isRunning) ExecuteSpawnBatch();
            }
        }

        // ── Lógica de spawn ───────────────────────────────────────────────

        private void SpawnOne()
        {
            if (_builder == null || _pool == null || !_pool.HasAvailable) return;

            if (_spawnPoints != null && _spawnPoints.Length > 0 && _playerTransform != null)
            {
                RebuildSortedSpawnPoints();
                if (_sortedSpawnPoints.Length == 0) return;

                Transform point = _sortedSpawnPoints[_sortedIndex % _sortedSpawnPoints.Length];
                _sortedIndex++;
                _builder.BuildAt(point.position);
            }
            else if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                Transform point = _spawnPoints[_spawnPointIndex % _spawnPoints.Length];
                _spawnPointIndex++;
                _builder.BuildAt(point.position);
            }
            else
            {
                _builder.Build();
            }
        }

        private void ExecuteSpawnBatch()
        {
            if (_builder == null || _pool == null) return;

            int toSpawn = Mathf.Min(_spawnBatchSize, _pool.AvailableCount);
            if (toSpawn == 0)
            {
                Debug.Log("[NpcEnemySpawner] Pool cheia. Aguardando retornos.");
                return;
            }

            if (_spawnPoints != null && _spawnPoints.Length > 0 && _playerTransform != null)
            {
                RebuildSortedSpawnPoints();

                if (_sortedSpawnPoints.Length == 0)
                {
                    Debug.LogWarning("[NpcEnemySpawner] Todos os spawn points dentro do raio de visão.");
                    return;
                }

                for (int i = 0; i < toSpawn; i++)
                {
                    Transform point = _sortedSpawnPoints[_sortedIndex % _sortedSpawnPoints.Length];
                    _sortedIndex++;
                    if (!_builder.BuildAt(point.position)) break;
                }
            }
            else if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                for (int i = 0; i < toSpawn; i++)
                {
                    Transform point = _spawnPoints[_spawnPointIndex % _spawnPoints.Length];
                    _spawnPointIndex++;
                    if (!_builder.BuildAt(point.position)) break;
                }
            }
            else
            {
                for (int i = 0; i < toSpawn; i++)
                    if (!_builder.Build()) break;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private void RebuildSortedSpawnPoints()
        {
            Vector3 playerPos      = _playerTransform.position;
            float   visionRadiusSq = _playerVisionRadius * _playerVisionRadius;

            int validCount = 0;
            var temp = new Transform[_spawnPoints.Length];

            foreach (var point in _spawnPoints)
            {
                if (point == null) continue;
                if ((point.position - playerPos).sqrMagnitude < visionRadiusSq) continue;
                temp[validCount++] = point;
            }

            // Insertion sort por distância
            for (int i = 1; i < validCount; i++)
            {
                var   key     = temp[i];
                float keyDist = (key.position - playerPos).sqrMagnitude;
                int   j       = i - 1;

                while (j >= 0 && (temp[j].position - playerPos).sqrMagnitude > keyDist)
                {
                    temp[j + 1] = temp[j];
                    j--;
                }

                temp[j + 1] = key;
            }

            if (_sortedSpawnPoints.Length != validCount)
                _sortedSpawnPoints = new Transform[validCount];

            System.Array.Copy(temp, _sortedSpawnPoints, validCount);

            if (_sortedSpawnPoints.Length > 0)
                _sortedIndex = _sortedIndex % _sortedSpawnPoints.Length;
        }

        // ── Gizmos ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_spawnPoints != null)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
                foreach (var p in _spawnPoints)
                {
                    if (p == null) continue;
                    Gizmos.DrawSphere(p.position, 0.4f);
                    Gizmos.DrawLine(p.position, p.position + Vector3.up * 2f);
                }
            }

            if (_playerTransform != null && _playerVisionRadius > 0f)
            {
                Gizmos.color = new Color(0f, 1f, 0.2f, 0.15f);
                Gizmos.DrawSphere(_playerTransform.position, _playerVisionRadius);
                Gizmos.color = new Color(0f, 1f, 0.2f, 0.6f);
                Gizmos.DrawWireSphere(_playerTransform.position, _playerVisionRadius);
            }
        }
#endif
    }
}