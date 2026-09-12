using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Engine;

public static class BattleFactory
{
    public static readonly string[] DefaultAllySkillIds = ["wulfric_innate_active1"];

    /// <summary>Skills always on Wulfric regardless of tree (innates).</summary>
    public static readonly string[] WulfricInnateSkillIds =
    [
        "wulfric_innate_active1",
        "wulfric_innate_active2",
        "wulfric_innate_active3",
        "wulfric_innate_active4",
    ];

    /// <summary>Innate loadout for Buck.</summary>
    public static readonly string[] BuckInnateSkillIds =
    [
        "buck_innate_active1",
        "buck_innate_active2",
        "buck_innate_active3",
        "buck_innate_active4",
    ];

    /// <summary>Innate loadout for Maria / The Star.</summary>
    public static readonly string[] MariaInnateSkillIds =
    [
        "maria_innate_active1",
        "maria_innate_active2",
        "maria_innate_active3",
        "maria_innate_active4",
    ];

    public static IReadOnlyList<string> ResolveInnateSkillIds(string progressionCharacterId)
    {
        if (string.Equals(progressionCharacterId, "buck", StringComparison.OrdinalIgnoreCase))
        {
            return BuckInnateSkillIds;
        }

        if (string.Equals(progressionCharacterId, "maria", StringComparison.OrdinalIgnoreCase))
        {
            return MariaInnateSkillIds;
        }

        if (string.Equals(progressionCharacterId, "wulfric", StringComparison.OrdinalIgnoreCase))
        {
            return WulfricInnateSkillIds;
        }

        return DefaultAllySkillIds;
    }

    public static IReadOnlyList<string> ResolveAlwaysOnPassiveIds(string progressionCharacterId)
    {
        if (string.Equals(progressionCharacterId, "wulfric", StringComparison.OrdinalIgnoreCase))
        {
            return WulfricAlwaysOnPassiveIds;
        }

        if (string.Equals(progressionCharacterId, "buck", StringComparison.OrdinalIgnoreCase))
        {
            return BuckAlwaysOnPassiveIds;
        }

        if (string.Equals(progressionCharacterId, "maria", StringComparison.OrdinalIgnoreCase))
        {
            return MariaAlwaysOnPassiveIds;
        }

        return [];
    }

    public static void UnlockAlwaysOnKitPassives(
        Combatant combatant,
        string progressionCharacterId,
        IReadOnlyDictionary<string, PassiveDefinition>? passivesById)
    {
        if (combatant == null || passivesById == null || passivesById.Count == 0)
        {
            return;
        }

        foreach (var passiveId in ResolveAlwaysOnPassiveIds(progressionCharacterId))
        {
            if (passivesById.ContainsKey(passiveId))
            {
                combatant.Progression.UnlockedNodes[passiveId] = true;
            }
        }
    }

    public static readonly string[] WulfricFullSkillLoadout =
    [
        "wulfric_innate_active1", "wulfric_innate_active2", "wulfric_innate_active3", "wulfric_innate_active4",
        "wulfric_tree1_tier1_active", "wulfric_tree1_tier2_active", "wulfric_tree1_tier3_active",
        "wulfric_tree2_tier1_active", "wulfric_tree2_tier2_active", "wulfric_tree2_tier3_active",
        "wulfric_tree3_tier1_active", "wulfric_tree3_tier2_active", "wulfric_tree3_tier3_active",
    ];

    public static readonly string[] WulfricAlwaysOnPassiveIds =
    [
        "wulfric_leader_passive1",
        "wulfric_companion_passive1",
        "wulfric_corruption_tier1_passive1",
        "wulfric_corruption_tier2_passive1",
        "wulfric_corruption_tier3_passive1",
    ];

    public static readonly string[] BuckFullSkillLoadout =
    [
        "buck_innate_active1", "buck_innate_active2", "buck_innate_active3", "buck_innate_active4",
        "buck_tree1_tier1_active", "buck_tree1_tier2_active", "buck_tree1_tier3_active",
        "buck_tree2_tier1_active", "buck_tree2_tier2_active", "buck_tree2_tier3_active",
        "buck_tree3_tier1_active", "buck_tree3_tier2_active", "buck_tree3_tier3_active",
    ];

    public static readonly string[] BuckAlwaysOnPassiveIds =
    [
        "buck_leader_passive1",
        "buck_companion_passive1",
        "buck_corruption_tier1_passive1",
        "buck_corruption_tier2_passive1",
        "buck_corruption_tier3_passive1",
    ];

    public static readonly string[] MariaFullSkillLoadout =
    [
        "maria_innate_active1", "maria_innate_active2", "maria_innate_active3", "maria_innate_active4",
        "maria_tree1_tier1_active", "maria_tree1_tier2_active", "maria_tree1_tier3_active",
        "maria_tree2_tier1_active", "maria_tree2_tier2_active", "maria_tree2_tier3_active",
        "maria_tree3_tier1_active", "maria_tree3_tier2_active", "maria_tree3_tier3_active",
    ];

    public static readonly string[] MariaAlwaysOnPassiveIds =
    [
        "maria_leader_passive1",
        "maria_companion_passive1",
        "maria_corruption_tier1_passive1",
        "maria_corruption_tier2_passive1",
        "maria_corruption_tier3_passive1",
    ];

    public static readonly string[] DefaultEnemySkillIds = ["spider_bite", "spider_web", "enemy_claw"];

    public static BattleState CreateSampleBattle(
        IReadOnlyList<SkillDefinition> skills,
        int allyCount = 2,
        int enemyCount = 4,
        double corruptionValue = 0,
        IReadOnlyList<string>? allySkillIds = null,
        IReadOnlyList<string>? enemySkillIds = null,
        IReadOnlyDictionary<string, PassiveDefinition>? passivesById = null,
        bool unlockAllPassiveNodesForAllies = false,
        IReadOnlyDictionary<string, EnemyDefinition>? enemyDefinitionsById = null)
    {
        var skillsById = skills.ToDictionary(skill => skill.Id, skill => skill);
        var passiveCatalog = passivesById ?? new Dictionary<string, PassiveDefinition>();

        var allySkills = allySkillIds ?? DefaultAllySkillIds;
        var foeSkills = enemySkillIds ?? DefaultEnemySkillIds;

        var allies = new List<Combatant>();
        for (var i = 0; i < allyCount; i++)
        {
            allies.Add(CreatePlayer($"ally_{i + 1}", i + 1, allySkills));
        }

        if (unlockAllPassiveNodesForAllies && passiveCatalog.Count > 0)
        {
            foreach (var ally in allies)
            {
                foreach (var passiveNodeId in passiveCatalog.Keys)
                {
                    ally.Progression.UnlockedNodes[passiveNodeId] = true;
                }
            }
        }

        var enemies = new List<Combatant>();
        for (var i = 0; i < enemyCount; i++)
        {
            enemies.Add(CreateEnemy($"enemy_{i + 1}", i + 1, foeSkills));
        }

        return new BattleState
        {
            Allies = allies,
            Enemies = enemies,
            SkillsById = skillsById,
            PassivesById = passiveCatalog,
            EnemyDefinitionsById = enemyDefinitionsById ??
                                   new Dictionary<string, EnemyDefinition>(StringComparer.OrdinalIgnoreCase),
            CorruptionValue = Math.Max(CorruptionRules.MinCorruptionValue, corruptionValue),
            BalanceConfig = CombatBalanceConfig.CreateDefault(),
            TurnNumber = 0,
            BattleId = Guid.NewGuid(),
        };
    }

    /// <summary>Marca todas as entradas de <paramref name="passivesById"/> como desbloqueadas em cada aliado (modo stress / regressão).</summary>
    public static void UnlockAllPassivesFromCatalog(
        BattleState battle,
        IReadOnlyDictionary<string, PassiveDefinition> passivesById)
    {
        if (passivesById.Count == 0)
        {
            return;
        }

        foreach (var ally in battle.Allies)
        {
            foreach (var passiveNodeId in passivesById.Keys)
            {
                ally.Progression.UnlockedNodes[passiveNodeId] = true;
            }
        }
    }

    private static Combatant CreatePlayer(string id, int rank, IReadOnlyList<string> skillIds)
    {
        var loadout = new SkillLoadoutComponent();
        foreach (var skillId in skillIds)
        {
            loadout.Skills.Add(skillId);
        }

        return new Combatant
        {
            Identity = new IdentityComponent
            {
                Id = id,
                DisplayName = id,
                Faction = Faction.Player,
                Tags = ["Player"],
            },
            Health = new HealthComponent
            {
                CurrentHp = 40,
                MaxHp = 40,
                IsDead = false,
                IsDeathblowPending = false,
            },
            Position = new PositionComponent
            {
                Side = Side.Allies,
                FrontRank = rank,
                Size = 1,
            },
            Stats = new StatsComponent
            {
                Speed = 6,
                Accuracy = 1.0,
                CritChance = 0.05,
            },
            Resistances = new ResistanceComponent
            {
                BurnRes = 0.15,
                BlightRes = 0.15,
                MoveRes = 0.15,
                StunRes = 0.15,
                DeathblowRes = 0.15,
            },
            Tokens = new TokenComponent(),
            Dots = new DotComponent(),
            SkillLoadout = loadout,
            Progression = new ProgressionComponent { Level = 0, SpentPoints = 0 },
            AI = null,
            ElementAffinity = new ElementAffinityComponent { Element = ElementType.Fire },
            PartyRole = CombatPartyRoleRules.FromAllyPartyIndex(rank - 1),
        };
    }

    private static Combatant CreateEnemy(string id, int rank, IReadOnlyList<string> skillIds)
    {
        var loadout = new SkillLoadoutComponent();
        foreach (var skillId in skillIds)
        {
            loadout.Skills.Add(skillId);
        }

        return new Combatant
        {
            Identity = new IdentityComponent
            {
                Id = id,
                DisplayName = id,
                Faction = Faction.Enemy,
                Tags = ["Enemy"],
            },
            Health = new HealthComponent
            {
                CurrentHp = 20,
                MaxHp = 20,
                IsDead = false,
                IsDeathblowPending = false,
            },
            Position = new PositionComponent
            {
                Side = Side.Enemies,
                FrontRank = rank,
                Size = 1,
            },
            Stats = new StatsComponent
            {
                Speed = 4,
                Accuracy = 1.0,
                CritChance = 0.03,
            },
            Resistances = new ResistanceComponent
            {
                BurnRes = 0.05,
                BlightRes = 0.05,
                MoveRes = 0.05,
                StunRes = 0.05,
                DeathblowRes = 0.05,
            },
            Tokens = new TokenComponent(),
            Dots = new DotComponent(),
            SkillLoadout = loadout,
            Progression = new ProgressionComponent { Level = 0, SpentPoints = 0 },
            AI = new AIComponent { DecisionPolicyId = "KillThenWeighted" },
            ElementAffinity = new ElementAffinityComponent { Element = ElementType.Anomaly },
            PartyRole = CombatantPartyRole.None,
        };
    }
}
