// ============================================================
// RestorativeDrink.cs
// Namespace : Core.Exploration.Items.Usables
// ============================================================
// Cura uma quantidade configurável de HP do Main e do Companion
// ao ser usado pelo jogador.
//
// Como ScriptableObject não tem acesso direto à cena, resolve as
// dependências via PlayableCharactersManager.Instance (singleton)
// — o mesmo padrão usado por PlayerProgressionService.
// ============================================================

using Core.Exploration.Items;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Exploration.Items.Usables
{
    [CreateAssetMenu(menuName = "Exploration/Items/Usable/Restorative Drink", fileName = "RestorativeDrink")]
    public sealed class RestorativeDrink : ScriptableObject, IItem
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Tooltip("Quantidade de HP restaurada em cada personagem ativo (Main e Companion).")]
        [Min(1f)]
        [SerializeField] private float _healAmount = 30f;

        // ── IStorageable ──────────────────────────────────────────────────

        public StorageMode storageMode => StorageMode.Stackable;

        // ── IItem ─────────────────────────────────────────────────────────

        public Sprite Sprite => spriteExposed;
        public Sprite spriteExposed;

        public void ExecuteItemEffect()
        {
            var manager = FindManager();
            if (manager == null) return;

            int healed = 0;

            // ── Main ──────────────────────────────────────────────────────
            if (manager.Main is PlayableCharacter main && main.HealthBar != null)
            {
                main.HealthBar.Heal(_healAmount);
                healed++;
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[RestorativeDrink] '{main.CharacterName}' curado em {_healAmount} HP " +
                    $"(atual: {main.HealthBar.CurrentHealth}/{main.HealthBar.MaxHealth}).",
                    LogCategory.Interaction);
            }

            // ── Companion ─────────────────────────────────────────────────
            if (manager.Companion is PlayableCharacter companion && companion.HealthBar != null)
            {
                companion.HealthBar.Heal(_healAmount);
                healed++;
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[RestorativeDrink] '{companion.CharacterName}' curado em {_healAmount} HP " +
                    $"(atual: {companion.HealthBar.CurrentHealth}/{companion.HealthBar.MaxHealth}).",
                    LogCategory.Interaction);
            }

            if (healed == 0)
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    "[RestorativeDrink] Nenhum personagem ativo encontrado para curar.",
                    LogCategory.Interaction);
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────

        private static PlayableCharactersManager FindManager()
        {
            var manager = Object.FindFirstObjectByType<PlayableCharactersManager>();
            if (manager != null) return manager;

            LoggerService.PrintLogMessage(LogLevel.Error,
                "[RestorativeDrink] PlayableCharactersManager não encontrado na cena.",
                LogCategory.Interaction);
            return null;
        }
    }
}