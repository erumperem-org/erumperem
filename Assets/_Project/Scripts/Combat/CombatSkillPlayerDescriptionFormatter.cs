using System.Collections.Generic;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Presentation;

namespace Erumperem.Combat
{
    /// <summary>
    /// Ponte Unity para <see cref="SkillPlayerDescriptionBuilder"/>.
    /// </summary>
    public static class CombatSkillPlayerDescriptionFormatter
    {
        public static string BuildSummaryLine(
            SkillDefinition skill,
            BattleState battleState = null,
            Combatant actor = null,
            Combatant previewTarget = null)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            return SkillPlayerDescriptionBuilder.BuildSummaryLine(skill, CreateContext(battleState, actor, previewTarget));
        }

        /// <summary>Só o essencial que as caixas de stats da HUD não mostram (efeitos, turno extra, passivas...).</summary>
        public static string BuildHudEssentialsLine(
            SkillDefinition skill,
            BattleState battleState = null,
            Combatant actor = null,
            Combatant previewTarget = null)
        {
            if (skill == null)
            {
                return string.Empty;
            }

            return SkillPlayerDescriptionBuilder.BuildHudEssentialsLine(skill, CreateContext(battleState, actor, previewTarget));
        }

        private static SkillPlayerDescriptionBuilder.SkillDescriptionContext CreateContext(
            BattleState battleState,
            Combatant actor,
            Combatant previewTarget)
        {
            SkillPlayerDescriptionBuilder.SkillDescriptionContext context = null;
            if (battleState != null || actor != null || previewTarget != null)
            {
                context = new SkillPlayerDescriptionBuilder.SkillDescriptionContext
                {
                    BattleState = battleState,
                    Actor = actor,
                    PreviewTarget = previewTarget,
                    SkillsById = battleState?.SkillsById as IReadOnlyDictionary<string, SkillDefinition>,
                };
            }

            return context;
        }
    }
}
