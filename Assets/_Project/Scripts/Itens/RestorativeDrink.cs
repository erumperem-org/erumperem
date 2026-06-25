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
        [SerializeField] private int _healAmount = 30;

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
            var manager = GameObject.FindFirstObjectByType<PlayableCharactersManager>();
            if (manager.Main is PlayableCharacter main)
            {
                main.HealthBar.Heal(_healAmount);
                main.definition.currentHitPoints = Mathf.Clamp(main.definition.currentHitPoints += _healAmount, 0, main.definition.MaxHitPoints);
            }
            if (manager.Companion is PlayableCharacter companion)
            {
                companion.HealthBar.Heal(_healAmount);
                companion.definition.currentHitPoints = Mathf.Clamp(companion.definition.currentHitPoints += _healAmount, 0, companion.definition.MaxHitPoints);
            }
            FindAnyObjectByType<CharacterViewHud>().RefreshAll();
        }
    }
}