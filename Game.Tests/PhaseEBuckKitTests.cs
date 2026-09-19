using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Config;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Core.Passives;
using Game.Core.Progression;

namespace Game.Tests;

public sealed class PhaseEBuckKitTests
{
    private const int CatalogRevolverDamageMinimum = 8;
    private const int CatalogRevolverDamageMaximum = 13;
    private const double TreeAccuracyBonus = 0.15;
    private const double CorruptionTier1AccuracyPenalty = -0.05;
    private const double CorruptionTier1CritChanceBonus = 0.03;
    private const double LeaderCritDamagePerCrit = 0.5;

    [Fact]
    public void Catalog_ContainsAllBuckKitIds()
    {
        var skills = SampleCombatData.CreateSkills().Select(skill => skill.Id).ToHashSet(StringComparer.Ordinal);
        var passives = SampleCombatData.CreatePassives().Select(passive => passive.Id).ToHashSet(StringComparer.Ordinal);
        var trees = CombatDataLoader.LoadSkillTrees(CombatDataLoader.ResolveDefaultSkillTreesPath());
        var buckTrees = SkillTreeLookup.FindCharacterTrees(trees, "buck");
        Assert.NotNull(buckTrees);

        foreach (var skillId in BattleFactory.BuckFullSkillLoadout)
        {
            Assert.True(skills.Contains(skillId), $"Missing Buck skill {skillId}");
        }

        foreach (var passiveId in BattleFactory.BuckAlwaysOnPassiveIds)
        {
            Assert.True(passives.Contains(passiveId), $"Missing Buck kit passive {passiveId}");
        }

        for (var treeIndex = 1; treeIndex <= 3; treeIndex++)
        {
            for (var tierIndex = 1; tierIndex <= 3; tierIndex++)
            {
                for (var passiveIndex = 1; passiveIndex <= 3; passiveIndex++)
                {
                    var treePassiveId = $"buck_tree{treeIndex}_tier{tierIndex}_passive{passiveIndex}";
                    Assert.True(passives.Contains(treePassiveId), $"Missing {treePassiveId}");
                    Assert.True(SkillTreeLookup.TryFindNode(buckTrees!, treePassiveId, out _, out _));
                }

                var treeActiveId = $"buck_tree{treeIndex}_tier{tierIndex}_active";
                Assert.True(SkillTreeLookup.TryFindNode(buckTrees!, treeActiveId, out _, out _));
            }
        }
    }

    [Fact]
    public void InnateRevolverShot_ExistsAndDealsCatalogDamageOnHit()
    {
        var skills = SampleCombatData.CreateSkills();
        var revolverShot = skills.First(skill => skill.Id == "buck_innate_active1");
        Assert.Equal(SkillTargetKind.OneEnemy, revolverShot.TargetKind);
        Assert.Equal(CatalogRevolverDamageMinimum, revolverShot.BaseDamage.Min);
        Assert.Equal(CatalogRevolverDamageMaximum, revolverShot.BaseDamage.Max);

        var battle = CreateBuckBattle(skills, allySkillIds: [revolverShot.Id]);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];
        var hpBefore = target.Health.CurrentHp;
        ResolveSkillWithoutCrit(battle, revolverShot, actor, target);

        var damageDealt = hpBefore - target.Health.CurrentHp;
        Assert.InRange(damageDealt, CatalogRevolverDamageMinimum, CatalogRevolverDamageMaximum);
    }

    [Fact]
    public void UnloadAmmo_HasHitCountThree()
    {
        var unload = SampleCombatData.CreateSkills().First(skill => skill.Id == "buck_innate_active3");
        Assert.Equal(3, unload.HitCount);
        Assert.Equal(SkillTargetKind.OneEnemy, unload.TargetKind);
    }

    [Fact]
    public void GunsForAll_FollowsUpInnatePistolRevolverAndRifle()
    {
        var gunsForAll = SampleCombatData.CreateSkills().First(skill => skill.Id == "buck_tree1_tier2_active");
        Assert.Equal(0, gunsForAll.Accuracy);
        Assert.Equal(SkillTargetKind.OneEnemy, gunsForAll.TargetKind);
        Assert.Equal(
            ["buck_innate_active2", "buck_innate_active3", "buck_innate_active4"],
            gunsForAll.FollowUpSkillIds);
    }

    [Fact]
    public void LeaderCritDamageStacking_AppliesOnlyWhenPartyRoleIsLeader()
    {
        var smack = CreateFixedDamageSkill("phase_e_leader_smack", damage: 10, critChance: 1);
        var passives = LoadBuckPassives("buck_leader_passive1");
        var battle = CreateBuckBattle(
            [smack],
            allyCount: 2,
            allySkillIds: [smack.Id],
            passivesById: passives,
            unlockPassiveIds: ["buck_leader_passive1"]);
        NeutralizeCombatantElements(battle);
        var leader = battle.Allies[0];
        var companion = battle.Allies[1];
        Assert.Equal(CombatantPartyRole.Leader, leader.PartyRole);
        Assert.Equal(CombatantPartyRole.Companion, companion.PartyRole);
        SetHealth(battle.Enemies[0], currentHp: 200, maxHp: 200);

        ResolveSkillAlwaysCrit(battle, smack, leader, battle.Enemies[0]);
        var leaderAfterFirstCrit = PassiveDataDrivenEngine.GetPermanentStatModifiers(
            battle,
            leader,
            battle.Enemies[0],
            smack);
        Assert.Equal(LeaderCritDamagePerCrit, leaderAfterFirstCrit.CritDamageAdditive, 5);

        ResolveSkillAlwaysCrit(battle, smack, leader, battle.Enemies[0]);
        var leaderAfterSecondCrit = PassiveDataDrivenEngine.GetPermanentStatModifiers(
            battle,
            leader,
            battle.Enemies[0],
            smack);
        Assert.Equal(LeaderCritDamagePerCrit * 2, leaderAfterSecondCrit.CritDamageAdditive, 5);

        ResolveSkillAlwaysCrit(battle, smack, companion, battle.Enemies[0]);
        var companionModifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(
            battle,
            companion,
            battle.Enemies[0],
            smack);
        Assert.Equal(0, companionModifiers.CritDamageAdditive);
    }

    [Fact]
    public void CompanionLuckyShot_AppliesOnlyWhenPartyRoleIsCompanion()
    {
        var skills = SampleCombatData.CreateSkills();
        var revolverShot = skills.First(skill => skill.Id == "buck_innate_active1");
        var passives = LoadBuckPassives("buck_companion_passive1");
        var battle = CreateBuckBattle(
            skills,
            allyCount: 2,
            allySkillIds: [revolverShot.Id],
            passivesById: passives,
            unlockPassiveIds: ["buck_companion_passive1"]);
        battle.PassiveTriggerRandom = new AlwaysSucceedingRandomSource();
        NeutralizeCombatantElements(battle);
        var leader = battle.Allies[0];
        var companion = battle.Allies[1];
        Assert.Equal(2, passives["buck_companion_passive1"].Effects[0].Stacks);
        companion.Tokens.Add(TokenType.BonusAction, 1);

        ResolveSkillWithoutCrit(battle, revolverShot, leader, battle.Enemies[0]);
        Assert.Equal(0, leader.Tokens.GetStacks(TokenType.LuckyShot));

        ResolveSkillWithoutCrit(battle, revolverShot, companion, battle.Enemies[0]);
        Assert.Equal(2, companion.Tokens.GetStacks(TokenType.LuckyShot));
        Assert.Equal(0, leader.Tokens.GetStacks(TokenType.LuckyShot));
    }

    [Fact]
    public void CorruptionTier1_AppliesAccuracyPenaltyAndCritChanceBonus()
    {
        var smack = CreateFixedDamageSkill("phase_e_corruption_smack", 10);
        var passives = LoadBuckPassives("buck_corruption_tier1_passive1");
        var battle = CreateBuckBattle(
            [smack],
            allySkillIds: [smack.Id],
            passivesById: passives,
            unlockPassiveIds: ["buck_corruption_tier1_passive1"],
            corruptionValue: CorruptionRules.Tier0UpperInclusive + 1);
        var buck = battle.Allies[0];
        var modifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(battle, buck, battle.Enemies[0], smack);
        Assert.Equal(CorruptionTier1AccuracyPenalty, modifiers.AccuracyAdditive, 5);
        Assert.Equal(CorruptionTier1CritChanceBonus, modifiers.CritChanceAdditive, 5);
    }

    [Fact]
    public void TreePassive_AccuracyPlusFifteenPercent_AppliesPermanently()
    {
        var smack = CreateFixedDamageSkill("phase_e_accuracy_smack", 10);
        var passives = LoadBuckPassives("buck_tree1_tier1_passive1");
        var battle = CreateBuckBattle(
            [smack],
            allySkillIds: [smack.Id],
            passivesById: passives,
            unlockPassiveIds: ["buck_tree1_tier1_passive1"]);
        var modifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(
            battle,
            battle.Allies[0],
            battle.Enemies[0],
            smack);
        Assert.Equal(TreeAccuracyBonus, modifiers.AccuracyAdditive, 5);
    }

    private static Dictionary<string, PassiveDefinition> LoadBuckPassives(params string[] passiveIds)
    {
        var catalog = SampleCombatData.CreatePassives().ToDictionary(passive => passive.Id, StringComparer.Ordinal);
        var selected = new Dictionary<string, PassiveDefinition>(StringComparer.Ordinal);
        foreach (var passiveId in passiveIds)
        {
            Assert.True(catalog.ContainsKey(passiveId), $"Catalog is missing {passiveId}");
            selected[passiveId] = catalog[passiveId];
        }

        return selected;
    }

    private static BattleState CreateBuckBattle(
        IReadOnlyList<SkillDefinition> skills,
        int allyCount = 1,
        int enemyCount = 1,
        IReadOnlyList<string>? allySkillIds = null,
        IReadOnlyList<string>? enemySkillIds = null,
        IReadOnlyDictionary<string, PassiveDefinition>? passivesById = null,
        IReadOnlyList<string>? unlockPassiveIds = null,
        double corruptionValue = 0)
    {
        var battle = BattleFactory.CreateSampleBattle(
            skills,
            allyCount,
            enemyCount,
            corruptionValue,
            allySkillIds,
            enemySkillIds,
            passivesById);
        if (unlockPassiveIds != null)
        {
            foreach (var ally in battle.Allies)
            {
                foreach (var passiveId in unlockPassiveIds)
                {
                    ally.Progression.UnlockedNodes[passiveId] = true;
                }
            }
        }

        return battle;
    }

    private static void SetHealth(Combatant combatant, int currentHp, int maxHp)
    {
        combatant.Health = new HealthComponent
        {
            CurrentHp = currentHp,
            MaxHp = maxHp,
            IsDead = false,
            IsDeathblowPending = false,
        };
    }

    private static void NeutralizeCombatantElements(BattleState battle)
    {
        foreach (var combatant in battle.GetAllCombatants())
        {
            combatant.ElementAffinity = new ElementAffinityComponent { Element = ElementType.None };
            combatant.Stats = new StatsComponent
            {
                Speed = combatant.Identity.Faction == Faction.Player ? 6 : 4,
                Accuracy = 1.0,
                CritChance = 0,
            };
        }
    }

    private static SkillDefinition CreateFixedDamageSkill(string skillId, int damage, double critChance = 0) =>
        new()
        {
            Id = skillId,
            Name = skillId,
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = damage, Max = damage },
            BaseCritChance = critChance,
            Accuracy = 1,
            TargetKind = SkillTargetKind.OneEnemy,
            EffectsOnHit = [],
        };

    private static void ResolveSkillWithoutCrit(
        BattleState battle,
        SkillDefinition skill,
        Combatant actor,
        Combatant target)
    {
        var simulator = new BattleSimulator(new HitWithoutCritRandomSource(), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = target,
                Skill = skill,
                ActionType = ActionType.Skill,
            });
    }

    private static void ResolveSkillAlwaysCrit(
        BattleState battle,
        SkillDefinition skill,
        Combatant actor,
        Combatant target)
    {
        var simulator = new BattleSimulator(new AlwaysHitAndCritRandomSource(), new CombatEventCollector());
        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = actor,
                Target = target,
                Skill = skill,
                ActionType = ActionType.Skill,
            });
    }

    private sealed class HitWithoutCritRandomSource : IRandomSource
    {
        private bool _nextDoubleIsCriticalStrikeRoll;

        public int Next(int minValue, int maxValue) => minValue;

        public double NextDouble()
        {
            if (!_nextDoubleIsCriticalStrikeRoll)
            {
                _nextDoubleIsCriticalStrikeRoll = true;
                return 0.0;
            }

            _nextDoubleIsCriticalStrikeRoll = false;
            return 0.99;
        }
    }

    private sealed class AlwaysHitAndCritRandomSource : IRandomSource
    {
        public int Next(int minValue, int maxValue) => minValue;

        public double NextDouble() => 0.0;
    }

    private sealed class AlwaysSucceedingRandomSource : IRandomSource
    {
        public int Next(int minValue, int maxValue) => minValue;

        public double NextDouble() => 0.0;
    }
}
