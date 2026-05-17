// ============================================================
// NpcEnemySpawner.cs
// Namespace : Systems.NPC.Spawner
// ============================================================
// Spawner de NPCs inimigos.
//
// Responsabilidades:
//   • Disparar spawns em intervalos configuráveis via Coroutine.
//   • Respeitar o limite máximo da pool.
//   • Expor controles de início/pausa/stop para sistemas externos.
//
// NÃO usa Update, FixedUpdate ou loops permanentes.
// Todo o ciclo de spawn é controlado por uma única Coroutine.
// ============================================================

using Systems.NPC.Builder;
using Systems.NPC.Pool;
using UnityEngine;
using System.Collections;

namespace Systems.NPC.Spawner
{
    /// <summary>
    /// Controla o fluxo de spawn de NPCs inimigos via Coroutine.
    ///
    /// Inicia/para spawning sob demanda. Respeita a capacidade
    /// da pool — não tenta spawnar quando a pool está cheia.
    ///
    /// Integração:
    ///   • Referencia NpcEnemyBuilder para criar NPCs.
    ///   • Referencia NpcEnemyPool para verificar disponibilidade.
    ///   • Pode receber spawn points externos ou usar o serviço do Builder.
    /// </summary>
    public sealed class NpcEnemySpawner : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Dependências")]
        [Tooltip("Builder responsável por construir e configurar os NPCs.")]
        [SerializeField] private NpcEnemyBuilder _builder;

        [Tooltip("Pool de referência para checar capacidade antes de spawnar.")]
        [SerializeField] private NpcEnemyPool _pool;

        [Header("Configuração de Spawn")]
        [Tooltip("Intervalo em segundos entre spawns automáticos.")]
        [SerializeField, Min(0.1f)] private float _spawnInterval = 5f;

        [Tooltip("Quantos NPCs spawnar por ciclo (limitado pela pool disponível).")]
        [SerializeField, Min(1)] private int _spawnBatchSize = 1;

        [Tooltip("Se true, inicia o spawn automático ao Awake.")]
        [SerializeField] private bool _autoStart = true;

        [Tooltip("Spawn points fixos. Se vazio, usa o SpawnPositionService do Builder.")]
        [SerializeField] private Transform[] _spawnPoints;

        [Tooltip("Referência ao Player. Se configurado, spawna em ordem do spawn point mais próximo ao mais longe, excluindo os que estão dentro do raio de visão.")]
        [SerializeField] private Transform _playerTransform;

        [Tooltip("Raio de visão do Player. Spawn points dentro deste raio são excluídos do batch.")]
        [SerializeField, Min(0f)] private float _playerVisionRadius = 10f;

        // ── Estado interno ────────────────────────────────────────────────

        private Coroutine  _spawnLoopCoroutine;
        private bool       _isRunning;
        private int        _spawnPointIndex;                       // round-robin (sem player)
        private Transform[] _sortedSpawnPoints = new Transform[0]; // ordenado por distância ao player
        private int        _sortedIndex;                           // cursor no array ordenado

        // ── Propriedades de consulta ──────────────────────────────────────

        public bool IsRunning => _isRunning;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_builder == null)
                Debug.LogError("[NpcEnemySpawner] NpcEnemyBuilder não configurado!", this);

            if (_pool == null)
                Debug.LogError("[NpcEnemySpawner] NpcEnemyPool não configurada!", this);
        }

        private void Start()
        {
            if (_autoStart)
                StartSpawning();
        }

        private void OnDestroy()
        {
            StopSpawning();
        }

        // ═════════════════════════════════════════════════════════════════
        // API pública — controle do spawner
        // ═════════════════════════════════════════════════════════════════

        /// <summary>Inicia o loop de spawn automático via Coroutine.</summary>
        public void StartSpawning()
        {
            if (_isRunning) return;

            _isRunning = true;
            _spawnLoopCoroutine = StartCoroutine(SpawnLoopCoroutine());
        }

        /// <summary>Para o loop de spawn. NPCs já ativos continuam funcionando.</summary>
        public void StopSpawning()
        {
            _isRunning = false;

            if (_spawnLoopCoroutine == null) return;
            StopCoroutine(_spawnLoopCoroutine);
            _spawnLoopCoroutine = null;
        }

        /// <summary>
        /// Spawna imediatamente um batch de NPCs, independente do timer.
        /// Útil para triggers de ondas ou eventos especiais.
        /// </summary>
        public void SpawnBatchNow()
        {
            ExecuteSpawnBatch();
        }

        /// <summary>
        /// Spawna um único NPC imediatamente no spawn point especificado.
        /// </summary>
        public void SpawnAt(Vector3 position)
        {
            if (_builder == null) return;
            _builder.BuildAt(position);
        }

        // ═════════════════════════════════════════════════════════════════
        // Coroutine de spawn loop
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Loop de spawn baseado em Coroutine.
        /// Executa um batch a cada _spawnInterval segundos.
        /// Encerrado por StopSpawning() ou OnDestroy.
        /// </summary>
        private IEnumerator SpawnLoopCoroutine()
        {
            // Spawn imediato na primeira execução, depois aguarda o intervalo
            ExecuteSpawnBatch();

            while (_isRunning)
            {
                yield return new WaitForSeconds(_spawnInterval);

                if (_isRunning)
                    ExecuteSpawnBatch();
            }
        }

        // ═════════════════════════════════════════════════════════════════
        // Lógica de spawn
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Executa um batch de spawns.
        /// Respeita o limite da pool e spawn points configurados.
        /// </summary>
        private void ExecuteSpawnBatch()
        {
            if (_builder == null || _pool == null) return;

            int toSpawn = Mathf.Min(_spawnBatchSize, _pool.AvailableCount);

            if (toSpawn == 0)
            {
                Debug.Log("[NpcEnemySpawner] Pool esgotada. Aguardando retornos.");
                return;
            }

            // Com Player referenciado e spawn points configurados:
            // reconstrói a lista ordenada por distância (excluindo pontos dentro do
            // raio de visão) e avança em sequência a cada NPC do batch.
            if (_spawnPoints != null && _spawnPoints.Length > 0 && _playerTransform != null)
            {
                RebuildSortedSpawnPoints();

                if (_sortedSpawnPoints.Length == 0)
                {
                    Debug.LogWarning("[NpcEnemySpawner] Todos os spawn points estão dentro do raio de visão do Player.");
                    return;
                }

                for (int i = 0; i < toSpawn; i++)
                {
                    // Avança em sequência pela lista já ordenada (mais próximo → mais longe).
                    // Reinicia quando chega ao fim para não travar em batches maiores que a lista.
                    Transform point = _sortedSpawnPoints[_sortedIndex % _sortedSpawnPoints.Length];
                    _sortedIndex++;

                    if (!_builder.BuildAt(point.position))
                    {
                        Debug.LogWarning("[NpcEnemySpawner] Falha ao spawnar NPC.");
                        break;
                    }
                }
            }
            else if (_spawnPoints != null && _spawnPoints.Length > 0)
            {
                // Sem player — round-robin original
                for (int i = 0; i < toSpawn; i++)
                {
                    Transform point = _spawnPoints[_spawnPointIndex % _spawnPoints.Length];
                    _spawnPointIndex++;

                    if (!_builder.BuildAt(point.position))
                    {
                        Debug.LogWarning("[NpcEnemySpawner] Falha ao spawnar NPC.");
                        break;
                    }
                }
            }
            else
            {
                // Sem spawn points — SpawnPositionService do Builder
                for (int i = 0; i < toSpawn; i++)
                {
                    if (!_builder.Build())
                    {
                        Debug.LogWarning("[NpcEnemySpawner] Falha ao spawnar NPC.");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Reconstrói <see cref="_sortedSpawnPoints"/> a cada batch:
        /// filtra os pontos dentro do raio de visão do Player e ordena os
        /// restantes do mais próximo ao mais distante.
        /// Reseta o cursor <see cref="_sortedIndex"/> para que o batch atual
        /// comece sempre pelo mais próximo disponível.
        /// </summary>
        private void RebuildSortedSpawnPoints()
        {
            Vector3 playerPos      = _playerTransform.position;
            float   visionRadiusSq = _playerVisionRadius * _playerVisionRadius;

            int validCount = 0;
            var temp = new Transform[_spawnPoints.Length];

            foreach (var point in _spawnPoints)
            {
                if (point == null) continue;
                float distSq = (point.position - playerPos).sqrMagnitude;
                if (distSq < visionRadiusSq) continue;
                temp[validCount++] = point;
            }

            // Insertion sort por distância ao Player
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

            // NÃO reseta _sortedIndex aqui — ele avança continuamente entre batches
            // para que cada batch use o próximo ponto da sequência, não o mesmo mais próximo.
            // Apenas mantém o índice dentro dos bounds do novo tamanho.
            if (_sortedSpawnPoints.Length > 0)
                _sortedIndex = _sortedIndex % _sortedSpawnPoints.Length;
        }

        // ═════════════════════════════════════════════════════════════════
        // Gizmo de debug — Spawn Points
        // ═════════════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_spawnPoints != null)
            {
                Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.8f);
                foreach (var point in _spawnPoints)
                {
                    if (point == null) continue;
                    Gizmos.DrawSphere(point.position, 0.4f);
                    Gizmos.DrawLine(point.position, point.position + Vector3.up * 2f);
                }
            }

            // Raio de visão do Player — mostra a zona de exclusão de spawn
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