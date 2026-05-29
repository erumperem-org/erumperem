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

            SkillPlayerDescriptionBuilder.SkillDescriptionContext context = null;
            if (battleState != null || actor != null || previewTarget != null)
            {
                context = new SkillPlayerDescriptionBuilder.SkillDescriptionContext
                {
                    BattleState = battleState,
                    Actor = actor,
                    PreviewTarget = previewTarget,
                };
            }

            return SkillPlayerDescriptionBuilder.BuildSummaryLine(skill, context);
        }
    }
}
