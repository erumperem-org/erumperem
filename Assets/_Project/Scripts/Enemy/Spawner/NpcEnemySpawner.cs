// ============================================================
// NpcEnemySpawner.cs
// Namespace : Systems.NPC.Spawner
// ============================================================
// Responsabilidade única: controlar o ciclo de respawn
// (quando e quantos NPCs spawnar).
//
// CORREÇÕES:
//   [1] _playerTransform é obtido dinamicamente via
//       PlayableCharactersManager.OnMainChanged, garantindo que
//       a troca de personagem Main atualize o selector.
//   [2] SpawnOneAtEachSpawnPoint agora usa o selector para
//       respeitar o raio de visão do player no spawn inicial.
//   [3] s_hasCompletedInitialSpawnFill virou instância:
//       ao retornar à cena o preenchimento inicial ocorre novamente.
//   [4] RebuildSelector é invocado sempre que o Main muda,
//       recriando PlayerAwareSpawnPointSelector com o novo transform.
//   [5] PlayerAwareSpawnPointSelector agora exige distância mínima
//       E máxima, garantindo que o player possa encontrar o inimigo.
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

        [Header("Referência ao Manager de personagens jogáveis")]
        [Tooltip("Se não atribuído, buscado automaticamente na cena.")]
        [SerializeField] private PlayableCharactersManager _playableCharactersManager;

        [Header("Spawn periódico")]
        [SerializeField, Min(0.1f)] private float _spawnInterval  = 5f;
        [SerializeField, Min(1)]    private int   _spawnBatchSize = 1;
        [SerializeField]            private bool  _autoStart      = true;

        [Header("Respawn após retorno à pool")]
        [SerializeField, Min(0f)] private float _respawnDelay = 3f;

        [Header("Spawn Points")]
        [SerializeField] private Transform[] _spawnPoints;

        [Header("Raios do Player")]
        [Tooltip("Inimigos não spawnão dentro deste raio (raio de visão).")]
        [SerializeField, Min(0f)] private float _playerMinSpawnRadius = 10f;

        [Tooltip("Inimigos não spawnão além deste raio (garante que o player possa encontrá-los). " +
                 "0 = sem limite máximo.")]
        [SerializeField, Min(0f)] private float _playerMaxSpawnRadius = 40f;

        // ── Estado interno ────────────────────────────────────────────────

        private const float SpawnOccupancyRadius = 2f;

        // [3] Removido o static — cada instância (cena) controla o seu próprio preenchimento.
        private bool _hasCompletedInitialSpawnFill;

        private Coroutine           _spawnLoopCoroutine;
        private bool                _isRunning;
        private ISpawnPointSelector _selector;

        // [1] Transform atual do personagem Main, atualizado via evento.
        private Transform _playerTransform;

        public bool IsRunning => _isRunning;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            ResolveDependencies();
            ResolveSpawnPointsIfNeeded();

            if (_builder == null) Debug.LogError("[NpcEnemySpawner] Builder não configurado!", this);
            if (_pool    == null) Debug.LogError("[NpcEnemySpawner] Pool não configurada!",    this);
        }

        private void OnEnable()
        {
            if (_pool != null) _pool.OnNpcReturned += HandleNpcReturned;

            // [1] Inscreve no evento de troca de Main.
            if (_playableCharactersManager != null)
                _playableCharactersManager.OnMainChanged += HandleMainChanged;
        }

        private void Start()
        {
            // [1] Inicializa o transform a partir do Main atual (se já existir).
            if (_playableCharactersManager != null)
                _playableCharactersManager.NotifyCurrentMainIfAny();

            RebuildSelector();

            if (_autoStart && !_isRunning)
                StartSpawning();
        }

        private void OnDisable()
        {
            if (_pool != null) _pool.OnNpcReturned -= HandleNpcReturned;

            if (_playableCharactersManager != null)
                _playableCharactersManager.OnMainChanged -= HandleMainChanged;

            StopSpawning();
        }

        private void OnDestroy() => StopSpawning();

        // ── Evento: troca de Main ─────────────────────────────────────────

        // [1][4][9] Atualiza o transform, reconstrói o selector e notifica o builder
        //           sempre que o Main muda — garante que todos os subsistemas
        //           usem o Transform correto do personagem ativo.
        private void HandleMainChanged(IPlayableCharacter newMain)
        {
            _playerTransform = newMain?.Transform;
            RebuildSelector();
            _builder?.SetPlayerTransform(_playerTransform);
        }

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

        public void SpawnBatchNow()           => ExecuteSpawnBatch();
        public void SpawnAt(Vector3 position) => _builder?.BuildAt(position);

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

            // [3] Instância: reseta ao entrar na cena, permitindo preenchimento no retorno.
            if (!_hasCompletedInitialSpawnFill)
            {
                SpawnOneAtEachSpawnPoint();
                _hasCompletedInitialSpawnFill = true;
            }

            while (_isRunning)
            {
                yield return new WaitForSeconds(_spawnInterval);
                if (_isRunning) ExecuteSpawnBatch();
            }
        }

        /// <summary>
        /// Preenche cada marcador de spawn com um NPC ao iniciar/retornar à cena.
        /// [2] Agora usa o selector para respeitar raio mínimo/máximo do player.
        /// </summary>
        private void SpawnOneAtEachSpawnPoint()
        {
            if (!_isRunning || !isActiveAndEnabled) return;
            if (_builder == null || _pool == null) return;

            // [2] Usa o selector (com filtro de visão) em vez de iterar direto nos pontos.
            if (_selector != null && _selector.HasAny)
            {
                int safeLimit = _spawnPoints?.Length ?? 0;
                for (int i = 0; i < safeLimit; i++)
                {
                    if (!_pool.HasAvailable) break;

                    var point = _selector.Next();
                    if (point == null) break;
                    if (_pool.HasActiveEnemyNear(point.position, SpawnOccupancyRadius)) continue;

                    _builder.BuildAt(point.position);
                }
            }
            else
            {
                // Sem player referenciado: fallback para iterar todos os pontos.
                Transform[] validSpawnPoints = FilterValidSpawnPoints(_spawnPoints);
                foreach (Transform spawnPoint in validSpawnPoints)
                {
                    if (!_pool.HasAvailable) break;
                    if (_pool.HasActiveEnemyNear(spawnPoint.position, SpawnOccupancyRadius)) continue;
                    _builder.BuildAt(spawnPoint.position);
                }
            }
        }

        // ── Lógica de spawn ───────────────────────────────────────────────

        private void SpawnOne()
        {
            if (_builder == null || _pool == null || !_pool.HasAvailable) return;

            var point = _selector?.Next();
            if (point != null)
            {
                if (_pool.HasActiveEnemyNear(point.position, SpawnOccupancyRadius)) return;
                _builder.BuildAt(point.position);
            }
            else if (_selector == null || !_selector.HasAny)
                _builder.Build();
            else
                Debug.LogWarning("[NpcEnemySpawner] Nenhum spawn point válido (fora dos raios min/max do player).");
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
                        Debug.LogWarning("[NpcEnemySpawner] Nenhum spawn point válido (fora dos raios min/max do player).");
                        break;
                    }
                    if (_pool.HasActiveEnemyNear(point.position, SpawnOccupancyRadius)) continue;
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
            if (_playableCharactersManager == null)
                _playableCharactersManager = FindFirstObjectByType<PlayableCharactersManager>();

            Transform enemySystemRoot = transform.parent;
            if (enemySystemRoot == null) return;

            if (_builder == null)
                _builder = enemySystemRoot.GetComponentInChildren<NpcEnemyBuilder>(true);

            if (_pool == null)
                _pool = enemySystemRoot.GetComponentInChildren<NpcEnemyPool>(true);
        }

        private void ResolveSpawnPointsIfNeeded()
        {
            Transform[] configuredSpawnPoints = FilterValidSpawnPoints(_spawnPoints);
            if (configuredSpawnPoints.Length > 0)
            {
                _spawnPoints = configuredSpawnPoints;
                return;
            }

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
                if (child.childCount > 0)
                {
                    CollectSpawnMarkers(child, results);
                    continue;
                }

                if (IsLeafSpawnMarker(child))
                    results.Add(child);
            }
        }

        // [4] Reconstrói selector sempre que chamado (Main mudou ou spawn points mudaram).
        private void RebuildSelector() => _selector = BuildSelector();

        // ── Factory do selector ───────────────────────────────────────────

        private ISpawnPointSelector BuildSelector()
        {
            Transform[] validSpawnPoints = FilterValidSpawnPoints(_spawnPoints);
            if (validSpawnPoints.Length == 0)
                return null;

            // [5] Passa raio máximo junto com o mínimo.
            return _playerTransform != null
                ? new PlayerAwareSpawnPointSelector(
                    validSpawnPoints,
                    _playerTransform,
                    _playerMinSpawnRadius,
                    _playerMaxSpawnRadius)
                : (ISpawnPointSelector) new RoundRobinSpawnPointSelector(validSpawnPoints);
        }

        private static Transform[] FilterValidSpawnPoints(Transform[] spawnPoints)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
                return System.Array.Empty<Transform>();

            var validSpawnPoints   = new System.Collections.Generic.List<Transform>(spawnPoints.Length);
            var seenSpawnPointIds  = new System.Collections.Generic.HashSet<int>();

            foreach (Transform spawnPoint in spawnPoints)
            {
                if (spawnPoint == null) continue;
                if (!IsLeafSpawnMarker(spawnPoint)) continue;
                if (!seenSpawnPointIds.Add(spawnPoint.GetInstanceID())) continue;
                validSpawnPoints.Add(spawnPoint);
            }

            return validSpawnPoints.ToArray();
        }

        private static bool IsLeafSpawnMarker(Transform spawnPoint)
        {
            if (spawnPoint == null) return false;
            if (!spawnPoint.name.Contains("ExplorationEnemySpawnPoint")) return false;

            foreach (Transform child in spawnPoint)
            {
                if (child.name.Contains("ExplorationEnemySpawnPoint"))
                    return false;
            }

            return true;
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

            if (_playerTransform != null)
            {
                // Raio mínimo (vermelho) — zona proibida de spawn.
                if (_playerMinSpawnRadius > 0f)
                {
                    Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.15f);
                    Gizmos.DrawSphere(_playerTransform.position, _playerMinSpawnRadius);
                    Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.6f);
                    Gizmos.DrawWireSphere(_playerTransform.position, _playerMinSpawnRadius);
                }

                // Raio máximo (verde) — limite de spawn alcançável.
                if (_playerMaxSpawnRadius > 0f)
                {
                    Gizmos.color = new Color(0f, 1f, 0.2f, 0.08f);
                    Gizmos.DrawSphere(_playerTransform.position, _playerMaxSpawnRadius);
                    Gizmos.color = new Color(0f, 1f, 0.2f, 0.4f);
                    Gizmos.DrawWireSphere(_playerTransform.position, _playerMaxSpawnRadius);
                }
            }
        }
#endif
    }
}
