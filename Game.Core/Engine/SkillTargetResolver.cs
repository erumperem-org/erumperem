using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Engine;

/// <summary>
/// Single source of truth for skill targeting.
/// Returns an ordered list of valid primary targets (Taunt, Stealth, and dead combatants applied).
/// PlayerActionBuilder, AI, preview, and HUD all call this.
/// </summary>
public static class SkillTargetResolver
{
    public const int UpToThreeEnemiesMaximumCount = 3;

    /// <summary>
    /// Resolves primary hit/damage targets for <paramref name="skill"/>.
    /// <see cref="SkillTargetKind.UpToThreeEnemies"/> = the selected enemy plus up to two other living
    /// valid enemies in presentation order (FrontRank ascending / left-to-right ranks), without a second click.
    /// </summary>
    public static IReadOnlyList<Combatant> ResolvePrimaryTargets(
        BattleState battleState,
        Combatant actor,
        SkillDefinition skill,
        Combatant? selectedCombatant)
    {
        if (battleState == null || actor == null || skill == null || actor.Health.IsDead)
        {
            return Array.Empty<Combatant>();
        }

        return skill.TargetKind switch
        {
            SkillTargetKind.Self => new[] { actor },
            SkillTargetKind.OneAlly => ResolveOneAlly(battleState, actor, selectedCombatant),
            SkillTargetKind.SelfOrAlly => ResolveSelfOrAlly(battleState, actor, selectedCombatant),
            SkillTargetKind.SelfAndAlly => ResolveSelfAndAlly(battleState, actor, skill),
            SkillTargetKind.OneEnemy => ResolveOneEnemy(battleState, actor, selectedCombatant),
            SkillTargetKind.UpToThreeEnemies => ResolveUpToThreeEnemies(battleState, actor, selectedCombatant),
            SkillTargetKind.AllEnemies => OrderByPresentation(
                GetValidEnemyPool(battleState, actor),
                OppositeSideRoster(battleState, actor)),
            _ => Array.Empty<Combatant>(),
        };
    }

    /// <summary>
    /// Combatants that receive an effect for the given <paramref name="effectScope"/> relative to a hit.
    /// <see cref="EffectScope.Default"/> = the primary hit target.
    /// </summary>
    public static IReadOnlyList<Combatant> ResolveEffectRecipients(
        BattleState battleState,
        Combatant actor,
        Combatant primaryHitTarget,
        EffectScope effectScope)
    {
        if (battleState == null || actor == null)
        {
            return Array.Empty<Combatant>();
        }

        return effectScope switch
        {
            EffectScope.Self => actor.Health.IsDead ? Array.Empty<Combatant>() : new[] { actor },
            EffectScope.AllAllies => LivingCombatantsOnRoster(SameSideRoster(battleState, actor)),
            EffectScope.AllEnemies => LivingCombatantsOnRoster(OppositeSideRoster(battleState, actor)),
            EffectScope.Default =>
                primaryHitTarget != null && !primaryHitTarget.Health.IsDead
                    ? new[] { primaryHitTarget }
                    : Array.Empty<Combatant>(),
            _ => Array.Empty<Combatant>(),
        };
    }

    /// <summary>
    /// Preferred click/hover combatant for HUD and eligibility, falling back to the first valid target.
    /// </summary>
    public static Combatant? ResolvePreferredSelection(
        BattleState battleState,
        Combatant actor,
        SkillDefinition skill,
        Combatant? preferredCombatant)
    {
        var resolvedPrimaryTargets = ResolvePrimaryTargets(battleState, actor, skill, preferredCombatant);
        if (resolvedPrimaryTargets.Count > 0)
        {
            if (preferredCombatant != null &&
                resolvedPrimaryTargets.Any(combatant =>
                    string.Equals(combatant.Identity.Id, preferredCombatant.Identity.Id, StringComparison.Ordinal)))
            {
                return preferredCombatant;
            }

            return resolvedPrimaryTargets[0];
        }

        if (SkillTargetKindRules.DirectsPrimaryDamageAtEnemies(skill.TargetKind))
        {
            foreach (var enemyCandidate in GetValidEnemyPool(battleState, actor))
            {
                var expanded = ResolvePrimaryTargets(battleState, actor, skill, enemyCandidate);
                if (expanded.Count > 0)
                {
                    return enemyCandidate;
                }
            }
        }

        if (SkillTargetKindRules.DirectsPrimaryDamageAtAllies(skill.TargetKind) ||
            SkillTargetKindRules.IsSelfOnly(skill.TargetKind))
        {
            foreach (var allyCandidate in LivingCombatantsOnRoster(SameSideRoster(battleState, actor)))
            {
                var expanded = ResolvePrimaryTargets(battleState, actor, skill, allyCandidate);
                if (expanded.Count > 0)
                {
                    return expanded[0];
                }
            }
        }

        return null;
    }

    public static int EstimatePrimaryTargetCount(
        BattleState battleState,
        Combatant actor,
        SkillDefinition skill)
    {
        if (battleState == null || actor == null || skill == null)
        {
            return 1;
        }

        return skill.TargetKind switch
        {
            SkillTargetKind.AllEnemies => Math.Max(1, GetValidEnemyPool(battleState, actor).Count),
            SkillTargetKind.UpToThreeEnemies => Math.Max(
                1,
                Math.Min(UpToThreeEnemiesMaximumCount, GetValidEnemyPool(battleState, actor).Count)),
            SkillTargetKind.SelfAndAlly => Math.Max(
                1,
                LivingCombatantsOnRoster(SameSideRoster(battleState, actor)).Count),
            _ => 1,
        };
    }

    public static IReadOnlyList<Combatant> GetValidEnemyPool(BattleState battleState, Combatant actor)
    {
        var oppositeRoster = OppositeSideRoster(battleState, actor);
        var livingEnemies = LivingCombatantsOnRoster(oppositeRoster);
        var tauntingEnemies = livingEnemies
            .Where(enemy => enemy.Tokens.GetStacks(TokenType.Taunt) > 0)
            .ToList();
        var candidateEnemies = tauntingEnemies.Count > 0 ? tauntingEnemies : livingEnemies;
        return candidateEnemies
            .Where(enemy => enemy.Tokens.GetStacks(TokenType.Stealth) == 0)
            .ToList();
    }

    private static IReadOnlyList<Combatant> ResolveOneAlly(
        BattleState battleState,
        Combatant actor,
        Combatant? selectedCombatant)
    {
        var visibleAllies = VisibleLivingSameSide(battleState, actor);
        if (selectedCombatant != null && visibleAllies.Contains(selectedCombatant))
        {
            return new[] { selectedCombatant };
        }

        if (visibleAllies.Contains(actor))
        {
            return new[] { actor };
        }

        return Array.Empty<Combatant>();
    }

    private static IReadOnlyList<Combatant> ResolveSelfOrAlly(
        BattleState battleState,
        Combatant actor,
        Combatant? selectedCombatant)
    {
        var visibleAllies = VisibleLivingSameSide(battleState, actor);
        if (selectedCombatant == null)
        {
            return visibleAllies.Contains(actor) ? new[] { actor } : Array.Empty<Combatant>();
        }

        if (selectedCombatant.Position.Side != actor.Position.Side)
        {
            return Array.Empty<Combatant>();
        }

        if (!visibleAllies.Contains(selectedCombatant))
        {
            return Array.Empty<Combatant>();
        }

        return new[] { selectedCombatant };
    }

    private static IReadOnlyList<Combatant> ResolveSelfAndAlly(BattleState battleState, Combatant actor, SkillDefinition skill)
    {
        var sameSideRoster = SameSideRoster(battleState, actor);
        var sameSideCombatants = skill.CanTargetDeadAllies
            ? sameSideRoster.ToList()
            : LivingCombatantsOnRoster(sameSideRoster);
        if (sameSideCombatants.Count == 0)
        {
            return Array.Empty<Combatant>();
        }

        var orderedSameSide = OrderByPresentation(sameSideCombatants, sameSideRoster);
        if (!orderedSameSide.Contains(actor))
        {
            return orderedSameSide;
        }

        var remainingAllies = orderedSameSide
            .Where(combatant => !string.Equals(combatant.Identity.Id, actor.Identity.Id, StringComparison.Ordinal))
            .ToList();
        var selfAndAllies = new List<Combatant> { actor };
        selfAndAllies.AddRange(remainingAllies);
        return selfAndAllies;
    }

    private static IReadOnlyList<Combatant> ResolveOneEnemy(
        BattleState battleState,
        Combatant actor,
        Combatant? selectedCombatant)
    {
        var validEnemies = GetValidEnemyPool(battleState, actor);
        if (selectedCombatant == null || !validEnemies.Contains(selectedCombatant))
        {
            return Array.Empty<Combatant>();
        }

        return new[] { selectedCombatant };
    }

    private static IReadOnlyList<Combatant> ResolveUpToThreeEnemies(
        BattleState battleState,
        Combatant actor,
        Combatant? selectedCombatant)
    {
        var validEnemies = GetValidEnemyPool(battleState, actor);
        if (selectedCombatant == null || !validEnemies.Contains(selectedCombatant))
        {
            return Array.Empty<Combatant>();
        }

        var additionalEnemies = OrderByPresentation(
                validEnemies.Where(enemy =>
                    !string.Equals(enemy.Identity.Id, selectedCombatant.Identity.Id, StringComparison.Ordinal)),
                OppositeSideRoster(battleState, actor))
            .Take(UpToThreeEnemiesMaximumCount - 1);

        var primaryTargets = new List<Combatant> { selectedCombatant };
        primaryTargets.AddRange(additionalEnemies);
        return primaryTargets;
    }

    private static List<Combatant> VisibleLivingSameSide(BattleState battleState, Combatant actor) =>
        LivingCombatantsOnRoster(SameSideRoster(battleState, actor))
            .Where(ally => ally.Tokens.GetStacks(TokenType.Stealth) == 0)
            .ToList();

    private static List<Combatant> LivingCombatantsOnRoster(IList<Combatant> roster) =>
        roster.Where(combatant => !combatant.Health.IsDead).ToList();

    private static IList<Combatant> SameSideRoster(BattleState battleState, Combatant actor) =>
        actor.Position.Side == Side.Allies ? battleState.Allies : battleState.Enemies;

    private static IList<Combatant> OppositeSideRoster(BattleState battleState, Combatant actor) =>
        actor.Position.Side == Side.Allies ? battleState.Enemies : battleState.Allies;

    private static List<Combatant> OrderByPresentation(
        IEnumerable<Combatant> combatants,
        IList<Combatant> roster)
    {
        return combatants
            .OrderBy(combatant => combatant.Position.FrontRank)
            .ThenBy(combatant => IndexOnRoster(roster, combatant))
            .ToList();
    }

    private static int IndexOnRoster(IList<Combatant> roster, Combatant combatant)
    {
        for (var rosterIndex = 0; rosterIndex < roster.Count; rosterIndex++)
        {
            if (string.Equals(roster[rosterIndex].Identity.Id, combatant.Identity.Id, StringComparison.Ordinal))
            {
                return rosterIndex;
            }
        }

        return int.MaxValue;
    }
}
