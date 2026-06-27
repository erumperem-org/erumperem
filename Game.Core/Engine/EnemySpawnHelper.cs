using System.Linq;
using Game.Core.Abstractions;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Passives;

namespace Game.Core.Engine;

/// <summary>Mid-battle enemy spawn into dead slots (ranks 1–4).</summary>
public static class EnemySpawnHelper
{
    private static readonly (double HpThreshold, int TierFlag)[] HpSummonTiers =
    [
        (0.75, 1),
        (0.50, 2),
        (0.25, 4),
    ];

    public static bool TryResolveEnemyTemplate(
        BattleState state,
        string archetypeId,
        out EnemyDefinition template)
    {
        if (!string.IsNullOrWhiteSpace(archetypeId) &&
            state.EnemyDefinitionsById.TryGetValue(archetypeId, out template!))
        {
            return true;
        }

        var snakeCaseId = ToSnakeCaseArchetypeId(archetypeId);
        if (!string.IsNullOrWhiteSpace(snakeCaseId) &&
            state.EnemyDefinitionsById.TryGetValue(snakeCaseId, out template!))
        {
            return true;
        }

        var fallbackTemplate = CreateFallbackTemplate(archetypeId);
        if (fallbackTemplate != null)
        {
            template = fallbackTemplate;
            return true;
        }

        template = null!;
        return false;
    }

    public static bool TrySpawnEnemyInDeadSlot(
        BattleState state,
        string archetypeId,
        IReadOnlyList<string> defaultSkillIds,
        IRandomSource random,
        out Combatant spawnedCombatant,
        out int rankUsed)
    {
        spawnedCombatant = null!;
        rankUsed = 0;

        if (!TryResolveEnemyTemplate(state, archetypeId, out var template))
        {
            return false;
        }

        var deadEnemySlots = state.Enemies.Where(enemy => enemy.Health.IsDead).ToList();
        if (deadEnemySlots.Count == 0)
        {
            return false;
        }

        var selectedSlotIndex = random.Next(0, deadEnemySlots.Count);
        var deadSlot = deadEnemySlots[selectedSlotIndex];
        ReinitializeCombatantFromTemplate(deadSlot, template, defaultSkillIds);
        spawnedCombatant = deadSlot;
        rankUsed = deadSlot.Position.FrontRank;
        return true;
    }

    /// <summary>
    /// For tiered summon passives: at most one tier per turn, lowest unconsumed threshold first (75 → 50 → 25).
    /// </summary>
    public static bool TryApplyHpTieredSummonPassive(
        BattleState state,
        Combatant actor,
        PassiveDefinition passiveDefinition,
        IRandomSource random,
        out Combatant spawnedCombatant,
        out int rankUsed,
        out int tierFlagUsed)
    {
        spawnedCombatant = null!;
        rankUsed = 0;
        tierFlagUsed = 0;

        if (passiveDefinition.EffectKind != PassiveEffectKind.SummonEnemyAtTurnStartWhenHpBelowTiered)
        {
            return false;
        }

        if (actor.Health.MaxHp <= 0)
        {
            return false;
        }

        var currentHpFraction = (double)actor.Health.CurrentHp / actor.Health.MaxHp;
        var archetypeId = passiveDefinition.SkillId ?? string.Empty;

        foreach (var (hpThreshold, tierFlag) in HpSummonTiers)
        {
            if (currentHpFraction >= hpThreshold)
            {
                continue;
            }

            if (actor.PassiveRuntime.WasHpTierSummonConsumed(tierFlag))
            {
                continue;
            }

            if (!TrySpawnEnemyInDeadSlot(
                    state,
                    archetypeId,
                    BattleFactory.DefaultEnemySkillIds,
                    random,
                    out spawnedCombatant,
                    out rankUsed))
            {
                return false;
            }

            actor.PassiveRuntime.MarkHpTierSummonConsumed(tierFlag);
            tierFlagUsed = tierFlag;
            return true;
        }

        return false;
    }

    private static void ReinitializeCombatantFromTemplate(
        Combatant combatant,
        EnemyDefinition template,
        IReadOnlyList<string> skillIds)
    {
        combatant.Identity = new IdentityComponent
        {
            Id = combatant.Identity.Id,
            DisplayName = template.Name,
            Faction = Faction.Enemy,
            Tags = ["Enemy", $"Archetype:{template.Id}"],
        };

        combatant.Health = new HealthComponent
        {
            CurrentHp = template.BaseHealth.CurrentHp,
            MaxHp = template.BaseHealth.MaxHp,
            IsDead = false,
            IsDeathblowPending = false,
        };

        combatant.Stats = template.BaseStats;
        combatant.Resistances = template.Resistances;
        combatant.ElementAffinity = new ElementAffinityComponent { Element = template.Element };
        combatant.Tokens = new TokenComponent();
        combatant.Dots = new DotComponent();
        combatant.AI ??= new AIComponent { DecisionPolicyId = template.AiPolicy };

        combatant.SkillLoadout.Skills.Clear();
        var skillsToAssign = template.Skills.Count > 0 ? template.Skills : skillIds;
        foreach (var skillId in skillsToAssign)
        {
            combatant.SkillLoadout.Skills.Add(skillId);
        }
    }

    private static EnemyDefinition? CreateFallbackTemplate(string archetypeId)
    {
        if (string.Equals(archetypeId, "CorruptedFairy", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(archetypeId, "corrupted_fairy", StringComparison.OrdinalIgnoreCase))
        {
            return CreateCorruptedFairyTemplate();
        }

        return null;
    }

    private static EnemyDefinition CreateCorruptedFairyTemplate() =>
        new()
        {
            Id = "corrupted_fairy",
            Name = "CorruptedFairy",
            Size = 1,
            BaseStats = new StatsComponent { Speed = 7, Accuracy = 1.0, CritChance = 0.04 },
            BaseHealth = new HealthComponent
            {
                CurrentHp = 18,
                MaxHp = 18,
                IsDead = false,
                IsDeathblowPending = false,
            },
            Resistances = new ResistanceComponent
            {
                BurnRes = 0.05,
                BlightRes = 0.05,
                MoveRes = 0.05,
                StunRes = 0.05,
                DeathblowRes = 0.05,
            },
            Skills = BattleFactory.DefaultEnemySkillIds.ToList(),
            AiPolicy = "KillThenWeighted",
            Element = ElementType.Anomaly,
        };

    private static string ToSnakeCaseArchetypeId(string archetypeId)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
        {
            return string.Empty;
        }

        if (string.Equals(archetypeId, "CorruptedFairy", StringComparison.OrdinalIgnoreCase))
        {
            return "corrupted_fairy";
        }

        return archetypeId;
    }
}
