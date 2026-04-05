using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;

namespace Game.Core.Engine;

public static class BattleFactory
{
    public static readonly string[] DefaultAllySkillIds = ["wulfric_innate_cleave"];

    public static readonly string[] WulfricFullSkillLoadout =
    [
        "wulfric_innate_cleave", "wulfric_innate_shove", "wulfric_innate_guard",
        "f_t1_a1", "f_t2_a1", "f_t3_a1",
        "m_t1_a1", "m_t2_a1", "m_t3_a1",
        "a_t1_a1", "a_t2_a1", "a_t3_a1",
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
        bool unlockAllPassiveNodesForAllies = false)
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
            CorruptionValue = Math.Clamp(corruptionValue, 0, 100),
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
        };
    }
}
