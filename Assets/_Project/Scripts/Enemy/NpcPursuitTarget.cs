// ============================================================
// NpcPursuitTarget.cs
// Namespace : Systems.NPC.Enemy
// ============================================================
// Handler global de alvo de perseguição.
//
// Centraliza o Transform do Player para que múltiplos NPCs
// possam acessá-lo sem acoplamento direto.
//
// Uso:
//   • PlayerDetectionSystem (ou similar) chama SetTarget() ao detectar o Player.
//   • NpcEnemy lê via NpcPursuitTarget.Current para validações extras.
//   • NpcEnemyConfig.PursuitTarget recebe o Transform diretamente do evento do Detector.
//
// Nota: O Detector do próprio NPC notifica OnDetectorEnter com o Collider do Player.
// Este handler é um complemento para sistemas externos que precisam do alvo global.
// ============================================================

using UnityEngine;

namespace Systems.NPC.Enemy
{
    /// <summary>
    /// Handler global e estático do alvo de perseguição dos NPCs.
    ///
    /// Não é um MonoBehaviour — é um serviço estático simples.
    /// Sem alocações, sem Update, sem dependências de Unity lifecycle.
    ///
    /// Sistemas externos (Player, GameManager) notificam mudanças via SetTarget/ClearTarget.
    /// </summary>
    public static class NpcPursuitTarget
    {
        // ── Estado ────────────────────────────────────────────────────────

        /// <summary>Transform atual do alvo global de perseguição (Player).</summary>
        public static Transform Current { get; private set; }

        /// <summary>True se há um alvo válido registrado.</summary>
        public static bool HasTarget => Current != null;

        // ── Eventos ───────────────────────────────────────────────────────

        /// <summary>
        /// Disparado quando o alvo global muda.
        /// NPCs podem se inscrever para reagir imediatamente a mudanças.
        /// </summary>
        public static event System.Action<Transform> OnTargetChanged;

        /// <summary>
        /// Disparado quando o alvo é removido (Player morreu, saiu, etc.).
        /// </summary>
        public static event System.Action OnTargetCleared;

        // ── API pública ───────────────────────────────────────────────────

        /// <summary>
        /// Define o alvo global de perseguição.
        /// Chamado pelo sistema do Player ao entrar no jogo.
        /// </summary>
        public static void SetTarget(Transform target)
        {
            Current = target;
            OnTargetChanged?.Invoke(target);
        }

        /// <summary>
        /// Remove o alvo global.
        /// Chamado quando o Player morre, sai do jogo, etc.
        /// </summary>
        public static void ClearTarget()
        {
            Current = null;
            OnTargetCleared?.Invoke();
        }
    }
}
