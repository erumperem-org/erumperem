using Game.Core.Models;

namespace Game.Core.Domain;

/// <summary>
/// Hypnosis: the combatant may only use the skill they resolved immediately before
/// this status was applied. Decays 1 stack at end of turn.
/// </summary>
public static class CombatHypnosisRules
{
    public static void CaptureLockFromLastResolvedSkill(Combatant combatant)
    {
        if (combatant.Tokens.GetStacks(TokenType.Hypnosis) <= 0)
        {
            combatant.PassiveRuntime.HypnosisLockedSkillId = null;
            return;
        }

        if (!string.IsNullOrEmpty(combatant.PassiveRuntime.HypnosisLockedSkillId))
        {
            return;
        }

        combatant.PassiveRuntime.HypnosisLockedSkillId = combatant.PassiveRuntime.LastResolvedSkillId;
    }

    public static void ClearLockIfExpired(Combatant combatant)
    {
        if (combatant.Tokens.GetStacks(TokenType.Hypnosis) <= 0)
        {
            combatant.PassiveRuntime.HypnosisLockedSkillId = null;
        }
    }

    public static bool IsSkillAllowed(Combatant combatant, SkillDefinition skill)
    {
        if (combatant.Tokens.GetStacks(TokenType.Hypnosis) <= 0)
        {
            return true;
        }

        var lockedSkillId = combatant.PassiveRuntime.HypnosisLockedSkillId;
        if (string.IsNullOrEmpty(lockedSkillId))
        {
            return false;
        }

        return string.Equals(lockedSkillId, skill.Id, StringComparison.Ordinal);
    }
}
