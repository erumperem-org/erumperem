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
        [SerializeField] private string _itemId;
        public string ItemId => _itemId;
                public string Description => _description;
        [SerializeField] private string _description;

        // ── IItem ─────────────────────────────────────────────────────────

        public Sprite Sprite => spriteExposed;

        public Sprite spriteExposed;

        public void ExecuteItemEffect()
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[RestorativeDrink] Cura desativada — volte à vila para recuperar HP.",
                LogCategory.Interaction);
        }
    }
}