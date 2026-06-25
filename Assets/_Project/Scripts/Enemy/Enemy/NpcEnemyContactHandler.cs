// ============================================================
// NpcEnemyContactHandler.cs
// Namespace : Systems.NPC.Enemy
// ============================================================
// Responsabilidade única: reagir ao contato NPC → Player.
//
// Centraliza o que antes estava duplicado em dois lugares:
//   • NpcEnemy.OnContactWithPlayer() → ScenesManager.LoadScene(...)
//   • EnemyCollissionTrigger         → SceneManager.LoadScene(...)
//
// O NpcEnemy dispara OnPlayerContact; este handler decide o
// que fazer, desacoplando o NPC de qualquer sistema de cena.
// ============================================================

using Systems.NPC.Enemy.Contracts;
using UnityEngine;
using UnityEngine.Events;

namespace Systems.NPC.Enemy
{
    /// <summary>
    /// Escuta OnPlayerContact de todos os NPCs gerenciados pela pool
    /// e executa a reação configurada no Inspector (UnityEvent).
    ///
    /// Troca o hardcode de "CombatScene" por uma referência configurável,
    /// evitando que o NPC conheça qualquer sistema externo.
    /// </summary>
    public sealed class NpcEnemyContactHandler : MonoBehaviour
    {
        private const string DefaultCombatSceneName = "CombatScene";

        [Header("Reação ao contato com o Player")]
        [Tooltip("Ações a executar quando um NPC toca o Player. " +
                 "Ex: ScenesManager.LoadSceneByName(\"CombatScene\")")]
        [SerializeField] private UnityEvent _onContact;

        // ── API pública ───────────────────────────────────────────────────

        /// <summary>
        /// Registra um NPC para que este handler ouça seus contatos.
        /// Chamado pelo NpcEnemyBuilder após Activate().
        /// </summary>
        public void Register(INpcEnemy enemy)
        {
            enemy.OnPlayerContact += HandleContact;
        }

        /// <summary>
        /// Remove o registro. Chamado quando o NPC retorna à pool.
        /// </summary>
        public void Unregister(INpcEnemy enemy)
        {
            enemy.OnPlayerContact -= HandleContact;
        }

        // ── Handler ───────────────────────────────────────────────────────

        private void HandleContact(INpcEnemy enemy)
        {
            Debug.Log($"[NpcEnemyContactHandler] Contato: '{(enemy as UnityEngine.Object)?.name}' → Player.");

            if (CombatExplorationBridge.IsCombatReentryBlocked)
                return;

            if (CombatExplorationBridge.AreExplorationCombatContactsBlocked)
                return;

            if (ExplorationVillageEvents.IsPlayerInsideVillage)
                return;

            CombatExplorationBridge.Instance?.NotifyEnteringCombat();

            if (HasConfiguredContactReaction())
                _onContact.Invoke();
            else
                SceneTransitionHandler.LoadScene(DefaultCombatSceneName);
        }

        private bool HasConfiguredContactReaction()
        {
            if (_onContact == null) return false;

            int persistentListenerCount = _onContact.GetPersistentEventCount();
            for (int listenerIndex = 0; listenerIndex < persistentListenerCount; listenerIndex++)
            {
                if (_onContact.GetPersistentTarget(listenerIndex) == null) continue;
                if (string.IsNullOrEmpty(_onContact.GetPersistentMethodName(listenerIndex))) continue;
                return true;
            }

            return false;
        }
    }
}
