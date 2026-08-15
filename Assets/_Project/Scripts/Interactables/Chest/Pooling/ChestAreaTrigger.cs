// ============================================================
// ChestAreaTrigger.cs
// Namespace : Systems.Chest.Spawner
// ============================================================
// Responsabilidade única: detectar quando o player entra ou
// sai da área e notificar o ChestAreaSpawner.
//
// Coloque este componente (junto com um Collider em modo Trigger)
// no GameObject da área. O ChestAreaSpawner pode ser no mesmo
// objeto ou referenciado via Inspector.
//
// Usa tag "Player" por padrão — ajuste _playerTag se necessário.
// ============================================================

using Services.DebugUtilities;
using Systems.Chest.Spawner;
using UnityEngine;

namespace Systems.Chest.Trigger
{
    [RequireComponent(typeof(Collider))]
    public sealed class ChestAreaTrigger : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Header("Dependências")]
        [SerializeField] private ChestAreaSpawner _spawner;

        [Header("Detecção")]
        [Tooltip("Tag do objeto que ativa o trigger. Geralmente 'Player'.")]
        [SerializeField] private string _playerTag = "Player";

        [Tooltip("Se verdadeiro, spawna os baús imediatamente no Start (sem precisar entrar no trigger).")]
        [SerializeField] private bool _spawnOnStart = false;

        // ── Unity Lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            if (_spawner == null)
                _spawner = GetComponent<ChestAreaSpawner>();

            if (_spawner == null)
                LoggerService.PrintLogMessage(LogLevel.Error,
                    $"[ChestAreaTrigger:{name}] ChestAreaSpawner não configurado!", LogCategory.Interaction);

            // Garante que o Collider desta área está em modo Trigger
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                col.isTrigger = true;
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    $"[ChestAreaTrigger:{name}] Collider convertido para Trigger automaticamente.",
                    LogCategory.Interaction);
            }
        }

        private void Start()
        {
            if (_spawnOnStart)
                _spawner?.OnAreaEntered();
        }

        // ── Detecção de trigger ───────────────────────────────────────────

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            if (_spawner == null) return;

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[ChestAreaTrigger:{name}] Player saiu. Devolvendo baús à pool.",
                LogCategory.Interaction);

            _spawner.OnAreaExited();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag(_playerTag)) return;
            if (_spawner == null) return;

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[ChestAreaTrigger:{name}] Player entrou. Populando área de baús.",
                LogCategory.Interaction);

            _spawner.OnAreaEntered();
        }

        // ── Gizmos ────────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = _spawner != null && _spawner.IsPopulated
                ? new Color(0.2f, 1f, 0.3f, 0.15f)   // verde = área populada
                : new Color(0.9f, 0.7f, 0.1f, 0.10f); // amarelo = área vazia

            var col = GetComponent<Collider>();
            if (col is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.7f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
        }
#endif
    }
}
