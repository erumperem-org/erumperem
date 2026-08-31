using Game.Core.Abstractions;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Engine;

/// <summary>Automatic skill and target selection for headless simulation and enemy AI.</summary>
internal sealed class BattleAiActionChooser
{
    private readonly IRandomSource _random;

    public BattleAiActionChooser(IRandomSource random)
    {
        _random = random;
    }

    public ChosenAction? ChooseAiAction(BattleState state, Combatant actor, Func<Combatant, SkillDefinition, bool> isSkillUsable)
    {
        var enemies = actor.Position.Side == Side.Allies ? state.Enemies : state.Allies;
        var availableEnemyTargets = enemies.Where(enemy => !enemy.Health.IsDead).ToList();
        if (availableEnemyTargets.Count == 0)
        {
            return null;
        }

        var availableSkills = actor.SkillLoadout.Skills
            .Where(id => state.SkillsById.ContainsKey(id))
            .Select(id => state.SkillsById[id])
            .Where(skill => isSkillUsable(actor, skill))
            .Where(skill =>
                actor.AI is null || SkillTargetKindRules.DirectsPrimaryDamageAtEnemies(skill.TargetKind))
            .ToList();

        if (availableSkills.Count == 0)
        {
            return null;
        }

        SkillDefinition selectedSkill;
        if (actor.AI?.DecisionPolicyId == "KillThenWeighted")
        {
            selectedSkill = ChooseEnemySkillForAi(state, actor, availableEnemyTargets, availableSkills);
        }
        else
        {
            selectedSkill = availableSkills[_random.Next(0, availableSkills.Count)];
        }

        var preferredSelection = PickPreferredSelection(state, actor, selectedSkill);
        var primaryTargets = SkillTargetResolver.ResolvePrimaryTargets(
            state,
            actor,
            selectedSkill,
            preferredSelection);
        if (primaryTargets.Count == 0)
        {
            return null;
        }

        return new ChosenAction
        {
            Actor = actor,
            Target = preferredSelection ?? primaryTargets[0],
            Skill = selectedSkill,
            ActionType = ActionType.Skill,
        };
    }

    private Combatant? PickPreferredSelection(BattleState state, Combatant actor, SkillDefinition selectedSkill)
    {
        if (SkillTargetKindRules.IsSelfOnly(selectedSkill.TargetKind) ||
            selectedSkill.TargetKind == SkillTargetKind.SelfAndAlly)
        {
            return actor;
        }

        if (selectedSkill.TargetKind == SkillTargetKind.OneAlly)
        {
            return SelectRandomVisibleAlly(state, actor, includeActor: false) ?? actor;
        }

        if (selectedSkill.TargetKind == SkillTargetKind.SelfOrAlly)
        {
            return SelectRandomVisibleAlly(state, actor, includeActor: true) ?? actor;
        }

        if (selectedSkill.TargetKind == SkillTargetKind.AllEnemies)
        {
            var validEnemies = SkillTargetResolver.GetValidEnemyPool(state, actor);
            return validEnemies.Count > 0 ? validEnemies[0] : null;
        }

        var selectableEnemies = SkillTargetResolver.GetValidEnemyPool(state, actor);
        if (selectableEnemies.Count == 0)
        {
            return null;
        }

        return selectableEnemies[_random.Next(0, selectableEnemies.Count)];
    }

    private Combatant? SelectRandomVisibleAlly(BattleState state, Combatant actor, bool includeActor)
    {
        var sameSideRoster = actor.Position.Side == Side.Allies ? state.Allies : state.Enemies;
        var visibleAllies = sameSideRoster
            .Where(ally => !ally.Health.IsDead && ally.Tokens.GetStacks(TokenType.Stealth) == 0)
            .Where(ally => includeActor || !string.Equals(ally.Identity.Id, actor.Identity.Id, StringComparison.Ordinal))
            .ToList();
        if (includeActor && visibleAllies.Count == 0)
        {
            return actor.Health.IsDead ? null : actor;
        }

        if (visibleAllies.Count == 0)
        {
            return null;
        }

        return visibleAllies[_random.Next(0, visibleAllies.Count)];
    }

    private SkillDefinition ChooseEnemySkillForAi(
        BattleState state,
        Combatant actor,
        IReadOnlyList<Combatant> targets,
        IReadOnlyList<SkillDefinition> skills)
    {
        var lethalSkills = new List<SkillDefinition>();
        foreach (var skill in skills)
        {
            foreach (var target in targets)
            {
                var estimate = EstimateDamage(state, actor, target, skill);
                if (estimate >= target.Health.CurrentHp)
                {
                    lethalSkills.Add(skill);
                    break;
                }
            }
        }

        var skillPool = lethalSkills.Count > 0 ? lethalSkills : skills.ToList();

        var rolledCandidates = new List<SkillDefinition>(skillPool.Count);
        foreach (var skill in skillPool)
        {
            var chance = Math.Clamp(skill.ChanceToUse, 0.0, 1.0);
            if (_random.NextDouble() < chance)
            {
                rolledCandidates.Add(skill);
            }
        }

        var finalPool = rolledCandidates.Count > 0 ? rolledCandidates : skillPool;
        var pickedIndex = _random.Next(0, finalPool.Count);
        return finalPool[pickedIndex];
    }

    private static int EstimateDamage(BattleState state, Combatant actor, Combatant target, SkillDefinition skill) =>
        CombatDamageCalculator.EstimateAverageDirectDamageOnHit(
            state,
            actor,
            target,
            skill,
            consumeMitigationTokens: false);
}
