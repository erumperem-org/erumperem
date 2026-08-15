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
        var availableTargets = enemies.Where(enemy => !enemy.Health.IsDead).ToList();
        if (availableTargets.Count == 0) return null;

        var availableSkills = actor.SkillLoadout.Skills
            .Where(id => state.SkillsById.ContainsKey(id))
            .Select(id => state.SkillsById[id])
            .Where(skill => isSkillUsable(actor, skill))
            .Where(skill => actor.AI is null || skill.TargetKind == SkillTargetKind.Enemy)
            .ToList();

        if (availableSkills.Count == 0) return null;

        SkillDefinition selectedSkill;
        if (actor.AI?.DecisionPolicyId == "KillThenWeighted")
        {
            selectedSkill = ChooseEnemySkillForAi(state, actor, availableTargets, availableSkills);
        }
        else
        {
            selectedSkill = availableSkills[_random.Next(0, availableSkills.Count)];
        }

        Combatant? target;
        if (selectedSkill.TargetKind == SkillTargetKind.Self)
        {
            target = actor;
        }
        else if (selectedSkill.TargetKind == SkillTargetKind.Ally)
        {
            var roster = actor.Position.Side == Side.Allies ? state.Allies : state.Enemies;
            var allies = roster.Where(combatant => !combatant.Health.IsDead).ToList();
            target = SelectAllyTarget(actor, allies, selectedSkill);
            if (target is null) return null;
        }
        else
        {
            target = SelectTarget(actor, availableTargets, selectedSkill);
            if (target is null) return null;
        }

        return new ChosenAction
        {
            Actor = actor,
            Target = target,
            Skill = selectedSkill,
            ActionType = ActionType.Skill,
        };
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

    private Combatant? SelectAllyTarget(
        Combatant _actor,
        IReadOnlyList<Combatant> allies,
        SkillDefinition _skill)
    {
        var visible = allies
            .Where(ally => ally.Tokens.GetStacks(TokenType.Stealth) == 0)
            .ToList();
        if (visible.Count == 0) return null;

        return visible[_random.Next(0, visible.Count)];
    }

    private Combatant? SelectTarget(
        Combatant _actor,
        IReadOnlyList<Combatant> availableTargets,
        SkillDefinition _skill)
    {
        var tauntTargets = availableTargets.Where(enemy => enemy.Tokens.GetStacks(TokenType.Taunt) > 0).ToList();
        var candidateTargets = tauntTargets.Count > 0 ? tauntTargets : availableTargets.ToList();

        var visibleTargets = candidateTargets
            .Where(enemy => enemy.Tokens.GetStacks(TokenType.Stealth) == 0)
            .ToList();
        if (visibleTargets.Count == 0)
        {
            return null;
        }

        return visibleTargets[_random.Next(0, visibleTargets.Count)];
    }

    private static int EstimateDamage(BattleState state, Combatant actor, Combatant target, SkillDefinition skill) =>
        CombatDamageCalculator.EstimateAverageDirectDamageOnHit(
            state,
            actor,
            target,
            skill,
            consumeMitigationTokens: false);
}
