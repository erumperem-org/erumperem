using UnityEngine;
using Core.Exploration.Items;
using Erumperem.Characters;
using Erumperem.Progression;
using Services.DebugUtilities;

namespace Core.CharacterStats
{
    /// <summary>
    /// Skill tree reset item. Effect is intentionally empty for now — the
    /// real skill tree system does not exist yet. The intended call is left
    /// commented below, ready to be wired in once that system is available.
    ///
    /// Create via: Assets → Create → Character Stats → Skill Tree Reset Item
    /// </summary>
    [CreateAssetMenu(menuName = "Character Stats/Skill Tree Reset Item", fileName = "SkillTreeReset_")]
    public sealed class SkillTreeResetItem : ItemDefinition
    {
        [SerializeField] private SkillTreeResetScope _scope = SkillTreeResetScope.General;
        public SkillTreeResetScope Scope => _scope;
        [Tooltip("Only used when Scope is CharacterSpecific. Placeholder id, same caveat as CharacterIdentityResolver.")]
        public AllyCharacterStatDefinition TargetCharacter;

        public override void ExecuteItemEffect()
        {
            var progression = PlayerProgressionService.Instance;
            if (progression == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error, "[SkillTreeResetItem] PlayerProgressionService não encontrado na cena.", LogCategory.Interaction);
                return;
            }
            if (Scope == SkillTreeResetScope.CharacterSpecific)
            {
                if (TargetCharacter == null)
                {
                    LoggerService.PrintLogMessage(LogLevel.Error, "[SkillTreeResetItem] TargetCharacter não definido para Scope CharacterSpecific.", LogCategory.Interaction);
                    return;
                }
                progression.ResetCharacter(TargetCharacter.CharacterId);
                return;
            }
            else
            {
                progression.ResetAllCharacters(); LoggerService.PrintLogMessage(LogLevel.Debug, "[SkillTreeResetItem] Arquivo de progressão inteiro resetado.", LogCategory.Interaction);
            }
        }
    }
}
