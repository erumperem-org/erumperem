// ============================================================
// ArtifactOfReturn.cs
// Namespace : Core.Exploration.Items.Usables
// ============================================================
// Reseta o arquivo de progressão inteiro ao ser usado.
// ============================================================

using Core.Exploration.Items;
using Erumperem.Progression;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Exploration.Items.Usables
{
    [CreateAssetMenu(menuName = "Exploration/Items/Usable/Artifact of Return", fileName = "ArtifactOfReturn")]
    public sealed class ArtifactOfReturn : ScriptableObject, IItem
    {
        // ── IStorageable ──────────────────────────────────────────────────

        public StorageMode storageMode => StorageMode.Unique;

        public Sprite Sprite => spriteExposed;

        [SerializeField] private string _itemId;
        public string ItemId => _itemId;

        [SerializeField] private string _description;
        public string Description => _description;

        public Sprite spriteExposed;

        // ── IItem ─────────────────────────────────────────────────────────

        public void ExecuteItemEffect()
        {
            var progression = PlayerProgressionService.Instance;
            if (progression == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error,
                    "[ArtifactOfReturn] PlayerProgressionService não encontrado na cena.",
                    LogCategory.Interaction);
                return;
            }

            progression.ResetAllCharacters();
            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[ArtifactOfReturn] Arquivo de progressão inteiro resetado.",
                LogCategory.Interaction);
        }
    }
}