// ============================================================
// ArtifactOfReturn.cs
// Namespace : Core.Exploration.Items.Usables
// ============================================================
// Reseta a skill tree do Main (e opcionalmente do Companion),
// devolvendo todos os pontos gastos para redistribuição.
//
// Usa PlayerProgressionService.Instance (singleton) para o reset
// — o mesmo padrão já adotado pelo serviço de progressão.
//
// Modo configurável via Inspector:
//   • ResetScope.MainOnly      → reseta apenas o Main
//   • ResetScope.CompanionOnly → reseta apenas o Companion
//   • ResetScope.Both          → reseta Main e Companion
//   • ResetScope.AllCharacters → reseta todos (ResetAllCharacters)
// ============================================================

using Core.Exploration.Items;
using Erumperem.Progression;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Exploration.Items.Usables
{
    public enum ResetScope
    {
        MainOnly,
        CompanionOnly,
        Both,
        AllCharacters
    }

    [CreateAssetMenu(menuName = "Exploration/Items/Usable/Artifact of Return", fileName = "ArtifactOfReturn")]
    public sealed class ArtifactOfReturn : ScriptableObject, IItem
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Tooltip("Quais personagens terão a build resetada ao usar o artefato.")]
        [SerializeField] private ResetScope _resetScope = ResetScope.Both;

        // ── IStorageable ──────────────────────────────────────────────────

        public StorageMode storageMode => StorageMode.Unique;

        public Sprite Sprite => spriteExposed;

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

            // ── AllCharacters: atalho direto, não precisa do manager ───────
            if (_resetScope == ResetScope.AllCharacters)
            {
                progression.ResetAllCharacters();
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    "[ArtifactOfReturn] Build de todos os personagens resetada.",
                    LogCategory.Interaction);
                return;
            }

            // ── Escopos por personagem ativo ───────────────────────────────
            var manager = Object.FindFirstObjectByType<PlayableCharactersManager>();
            if (manager == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error,
                    "[ArtifactOfReturn] PlayableCharactersManager não encontrado na cena.",
                    LogCategory.Interaction);
                return;
            }

            bool resetMain      = _resetScope is ResetScope.MainOnly or ResetScope.Both;
            bool resetCompanion = _resetScope is ResetScope.CompanionOnly or ResetScope.Both;

            if (resetMain && manager.Main is PlayableCharacter main)
            {
                progression.ResetCharacter(main.CharacterName);
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[ArtifactOfReturn] Build de '{main.CharacterName}' (Main) resetada. " +
                    $"Pontos devolvidos: {progression.MaxSkillPoints}.",
                    LogCategory.Interaction);
            }

            if (resetCompanion && manager.Companion is PlayableCharacter companion)
            {
                progression.ResetCharacter(companion.CharacterName);
                LoggerService.PrintLogMessage(LogLevel.Debug,
                    $"[ArtifactOfReturn] Build de '{companion.CharacterName}' (Companion) resetada. " +
                    $"Pontos devolvidos: {progression.MaxSkillPoints}.",
                    LogCategory.Interaction);
            }
        }
    }
}