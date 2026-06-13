// ============================================================
// NpcEnemySpawner.cs
// Namespace : Systems.NPC.Spawner
// ============================================================
// Responsabilidade única: controlar o ciclo de respawn
// (quando e quantos NPCs spawnar).
//
// A decisão de ONDE spawnar foi extraída para ISpawnPointSelector
// (PlayerAwareSpawnPointSelector ou RoundRobinSpawnPointSelector).
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
        [SerializeField, Min(0.1f)] private float _spawnInterval  = 5f;
        [SerializeField, Min(1)]    private int   _spawnBatchSize = 1;
        [SerializeField]            private bool  _autoStart      = true;

        [Header("Respawn após retorno à pool")]
        [SerializeField, Min(0f)] private float _respawnDelay = 3f;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] _spawnPoints;
        [SerializeField] private Transform   _playerTransform;
        [SerializeField, Min(0f)] private float _playerVisionRadius = 10f;

        // ── Estado interno ────────────────────────────────────────────────

        private Coroutine          _spawnLoopCoroutine;
        private bool               _isRunning;
        private ISpawnPointSelector _selector;

        public bool IsRunning => _isRunning;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            ResolveDependencies();
            ResolveSpawnPointsIfNeeded();
            RebuildSelector();

            if (_builder == null) Debug.LogError("[NpcEnemySpawner] Builder não configurado!", this);
            if (_pool    == null) Debug.LogError("[NpcEnemySpawner] Pool não configurada!",    this);
        }

        private void OnEnable()
        {
            if (_pool != null) _pool.OnNpcReturned += HandleNpcReturned;

            if (_autoStart && !_isRunning)
                StartSpawning();
        }

        private void OnDisable()
        {
            if (_pool != null) _pool.OnNpcReturned -= HandleNpcReturned;
            StopSpawning();
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

        public void SpawnBatchNow()            => ExecuteSpawnBatch();
        public void SpawnAt(Vector3 position)  => _builder?.BuildAt(position);

        // ── Respawn reativo ───────────────────────────────────────────────

        private void HandleNpcReturned()
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(RespawnAfterDelay());
        }

        private IEnumerator RespawnAfterDelay()
        {
            if (_respawnDelay > 0f)
                yield return new WaitForSeconds(_respawnDelay);

            if (!_isRunning || !isActiveAndEnabled) yield break;
            if (_pool != null && _pool.HasAvailable)
                SpawnOne();
        }

        // ── Loop periódico ────────────────────────────────────────────────

        private IEnumerator SpawnLoopCoroutine()
        {
            yield return null;

            ResolveSpawnPointsIfNeeded();
            RebuildSelector();
            SpawnOneAtEachSpawnPoint();

            while (_isRunning)
            {
                yield return new WaitForSeconds(_spawnInterval);
                if (_isRunning) ExecuteSpawnBatch();
            }
        }

        /// <summary>
        /// Preenche cada marcador de spawn com um NPC ao iniciar o jogo.
        /// </summary>
        private void SpawnOneAtEachSpawnPoint()
        {
            if (!_isRunning || !isActiveAndEnabled) return;
            if (_builder == null || _pool == null) return;

            Transform[] validSpawnPoints = FilterValidSpawnPoints(_spawnPoints);
            foreach (Transform spawnPoint in validSpawnPoints)
            {
                if (!_pool.HasAvailable) break;
                _builder.BuildAt(spawnPoint.position);
            }
        }

        // ── Lógica de spawn ───────────────────────────────────────────────

        private void SpawnOne()
        {
            if (_builder == null || _pool == null || !_pool.HasAvailable) return;

            var point = _selector?.Next();
            if (point != null)
                _builder.BuildAt(point.position);
            else if (_selector == null || !_selector.HasAny)
                _builder.Build();
            else
                Debug.LogWarning("[NpcEnemySpawner] Todos os spawn points dentro do raio de visão.");
        }

        private void ExecuteSpawnBatch()
        {
            if (!_isRunning || !isActiveAndEnabled) return;
            if (_builder == null || _pool == null) return;

            int toSpawn = Mathf.Min(_spawnBatchSize, _pool.AvailableCount);
            if (toSpawn == 0)
            {
                Debug.Log("[NpcEnemySpawner] Pool cheia. Aguardando retornos.");
                return;
            }

            if (_selector != null && _selector.HasAny)
            {
                for (int i = 0; i < toSpawn; i++)
                {
                    var point = _selector.Next();
                    if (point == null)
                    {
                        Debug.LogWarning("[NpcEnemySpawner] Todos os spawn points dentro do raio de visão.");
                        break;
                    }
                    if (!_builder.BuildAt(point.position)) break;
                }
            }
            else
            {
                for (int i = 0; i < toSpawn; i++)
                    if (!_builder.Build()) break;
            }
        }

        // ── Resolução de dependências ─────────────────────────────────────

        private void ResolveDependencies()
        {
            Transform enemySystemRoot = transform.parent;
            if (enemySystemRoot == null) return;

            if (_builder == null)
                _builder = enemySystemRoot.GetComponentInChildren<NpcEnemyBuilder>(true);

            if (_pool == null)
                _pool = enemySystemRoot.GetComponentInChildren<NpcEnemyPool>(true);
        }

        private void ResolveSpawnPointsIfNeeded()
        {
            if (FilterValidSpawnPoints(_spawnPoints).Length > 0)
                return;

            Transform enemySystemRoot = transform.parent;
            if (enemySystemRoot == null) return;

            Transform spawnPointsParent = enemySystemRoot.Find("Enemy Spawn Points");
            if (spawnPointsParent == null) return;

            var discoveredSpawnPoints = new System.Collections.Generic.List<Transform>();
            CollectSpawnMarkers(spawnPointsParent, discoveredSpawnPoints);

            if (discoveredSpawnPoints.Count == 0) return;

            _spawnPoints = discoveredSpawnPoints.ToArray();
        }

        private static void CollectSpawnMarkers(Transform parent, System.Collections.Generic.List<Transform> results)
        {
            foreach (Transform child in parent)
            {
                if (IsLeafSpawnMarker(child))
                    results.Add(child);

                if (child.childCount > 0)
                    CollectSpawnMarkers(child, results);
            }
        }

        private void RebuildSelector() => _selector = BuildSelector();

        // ── Factory do selector ───────────────────────────────────────────

        private ISpawnPointSelector BuildSelector()
        {
            Transform[] validSpawnPoints = FilterValidSpawnPoints(_spawnPoints);
            if (validSpawnPoints.Length == 0)
                return null;

            return _playerTransform != null
                ? new PlayerAwareSpawnPointSelector(validSpawnPoints, _playerTransform, _playerVisionRadius)
                : (ISpawnPointSelector) new RoundRobinSpawnPointSelector(validSpawnPoints);
        }

        /// <summary>
        /// Ignora entradas nulas e transforms-pai (ex.: "Enemy Spawn Points")
        /// que não são marcadores <c>ExplorationEnemySpawnPoint</c>.
        /// </summary>
        private static Transform[] FilterValidSpawnPoints(Transform[] spawnPoints)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return System.Array.Empty<Transform>();

            var validSpawnPoints = new System.Collections.Generic.List<Transform>(spawnPoints.Length);
            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint == null) continue;
                if (!IsLeafSpawnMarker(spawnPoint)) continue;
                validSpawnPoints.Add(spawnPoint);
            }

            return validSpawnPoints.ToArray();
        }

        private static bool IsLeafSpawnMarker(Transform spawnPoint)
        {
            if (spawnPoint.name.Contains("ExplorationEnemySpawnPoint"))
                return true;

            foreach (Transform child in spawnPoint)
            {
                if (child.name.Contains("ExplorationEnemySpawnPoint"))
                    return false;
            }

            return spawnPoint.childCount == 0;
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
