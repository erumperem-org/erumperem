namespace Game.Core.Domain;

/// <summary>Classification helpers for <see cref="SkillTargetKind"/> (selection vs primary damage).</summary>
public static class SkillTargetKindRules
{
    public static bool DirectsPrimaryDamageAtEnemies(SkillTargetKind targetKind) =>
        targetKind is SkillTargetKind.OneEnemy
            or SkillTargetKind.UpToThreeEnemies
            or SkillTargetKind.AllEnemies;

    public static bool DirectsPrimaryDamageAtAllies(SkillTargetKind targetKind) =>
        targetKind is SkillTargetKind.OneAlly
            or SkillTargetKind.SelfOrAlly
            or SkillTargetKind.SelfAndAlly;

    public static bool IsSelfOnly(SkillTargetKind targetKind) =>
        targetKind == SkillTargetKind.Self;
}
