// ============================================================
// ChestAreaSpawner.cs
// Namespace : Systems.Chest.Spawner
// ============================================================
// Responsabilidade única: gerenciar o ciclo de vida dos baús
// dentro de uma área — spawn ao entrar, retorno ao sair.
//
// Espelha o padrão de NpcEnemySpawner:
//   • Depende de ChestBuilder (monta) e ChestPool (disponibilidade).
//   • SpawnPoints definidos via Inspector (Transforms filhos ou externos).
//   • Ao entrar na área  → SpawnAll() aloca um baú por spawn point.
//   • Ao sair da área   → ReturnAll() devolve todos à pool.
//
// Uso típico: coloque no mesmo GameObject do Collider de área (trigger).
// O ChestAreaTrigger notifica este spawner via OnAreaEntered/OnAreaExited.
// ============================================================

using System.Collections.Generic;
using Core.Exploration.Interactables.Chest;
using Services.DebugUtilities;
using Systems.Chest.Builder;
using Systems.Chest.Pool;
using UnityEngine;

namespace Systems.Chest.Spawner
{
    public sealed class ChestAreaSpawner : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Dependências")]
        [SerializeField] private ChestBuilder _builder;
        [SerializeField] private ChestPool    _pool;

        [Header("Spawn Points")]
        [Tooltip("Um baú será alocado para cada Transform desta lista ao entrar na área.")]
        [SerializeField] private List<Transform> _spawnPoints = new();

        [Header("Comportamento")]
        [Tooltip("Se verdadeiro, baús abertos também são devolvidos ao sair da área.")]
        [SerializeField] private bool _returnOpenedChestsOnExit = true;

        // ── Estado interno ────────────────────────────────────────────────

        /// <summary>Mapeia spawn point → baú atualmente alocado.</summary>
        private readonly Dictionary<Transform, ChestInteractable> _activeChests = new();

        public bool IsPopulated => _activeChests.Count > 0;

        // ── API pública ───────────────────────────────────────────────────

        /// <summary>
        /// Chamado pelo ChestAreaTrigger quando o player entra na área.
        /// Aloca um baú por spawn point disponível.
        /// </summary>
        public void OnAreaEntered()
        {
            if (_activeChests.Count > 0)
            {
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[ChestAreaSpawner:{name}] Área já populada. Ignorando entrada duplicada.",
                    LogCategory.Interaction);
                return;
            }

            SpawnAll();
        }

        /// <summary>
        /// Chamado pelo ChestAreaTrigger quando o player sai da área.
        /// Devolve todos os baús à pool (resetando-os e sorteando novo loot na próxima entrada).
        /// </summary>
        public void OnAreaExited()
        {
            ReturnAll();
        }

        /// <summary>
        /// Força a reciclagem imediata de todos os baús (útil para recarregar a área via código).
        /// </summary>
        public void ForceRefresh()
        {
            ReturnAll();
            SpawnAll();
        }

        // ── Spawn / Return ────────────────────────────────────────────────

        private void SpawnAll()
        {
            if (_builder == null || _pool == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error,
                    $"[ChestAreaSpawner:{name}] Builder ou Pool não configurados!", LogCategory.Interaction);
                return;
            }

            int spawned = 0;

            foreach (var point in _spawnPoints)
            {
                if (point == null) continue;
                if (_activeChests.ContainsKey(point)) continue;
                if (!_pool.HasAvailable)
                {
                    LoggerService.PrintLogMessage(LogLevel.Warning,
                        $"[ChestAreaSpawner:{name}] Pool esgotada. {spawned} baú(s) alocado(s).",
                        LogCategory.Interaction);
                    break;
                }

                var chest = _builder.BuildAt(point.position, point.rotation);
                if (chest == null) continue;

                _activeChests[point] = chest;
                spawned++;
            }

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[ChestAreaSpawner:{name}] {spawned} baú(s) alocado(s) para a área.",
                LogCategory.Interaction);
        }

        private void ReturnAll()
        {
            if (_activeChests.Count == 0) return;

            int returned = 0;

            foreach (var kvp in _activeChests)
            {
                var chest = kvp.Value;
                if (chest == null) continue;

                // Respeita a flag: se o baú foi aberto e não queremos retornar abertos, pula.
                if (!_returnOpenedChestsOnExit && chest.IsOpened) continue;

                _builder.ReturnToPool(chest);
                returned++;
            }

            _activeChests.Clear();

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[ChestAreaSpawner:{name}] {returned} baú(s) devolvido(s) à pool.",
                LogCategory.Interaction);
        }

        // ── Gizmos ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_spawnPoints == null) return;

            foreach (var p in _spawnPoints)
            {
                if (p == null) continue;

                bool isActive = _activeChests.ContainsKey(p);

                Gizmos.color = isActive
                    ? new Color(0.2f, 1f, 0.3f, 0.9f)   // verde = ocupado
                    : new Color(0.9f, 0.7f, 0.1f, 0.8f); // amarelo = livre

                Gizmos.DrawWireCube(p.position, new Vector3(0.6f, 0.6f, 0.6f));
                Gizmos.DrawLine(p.position, p.position + Vector3.up * 1.5f);
            }
        }
#endif
    }
}
