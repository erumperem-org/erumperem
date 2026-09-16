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

public sealed class PhaseDWulfricKitTests
{
    private const int CatalogBasicDamageMinimum = 6;
    private const int CatalogBasicDamageMaximum = 10;
    private const int CompanionMissingHitPointsPerDefenseChunk = 18;
    private const double CompanionDefensePerMissingChunk = 0.02;
    private const double CorruptionTier1InitialDefensePenalty = -0.05;
    private const double CorruptionTier1OnHitDefenseBonus = 0.06;

    [Fact]
    public void Catalog_ContainsAllWulfricKitIds()
    {
        var skills = SampleCombatData.CreateSkills().Select(skill => skill.Id).ToHashSet(StringComparer.Ordinal);
        var passives = SampleCombatData.CreatePassives().Select(passive => passive.Id).ToHashSet(StringComparer.Ordinal);
        var trees = CombatDataLoader.LoadSkillTrees(CombatDataLoader.ResolveDefaultSkillTreesPath());
        var wulfricTrees = SkillTreeLookup.FindCharacterTrees(trees, "wulfric");
        Assert.NotNull(wulfricTrees);

        foreach (var skillId in BattleFactory.WulfricFullSkillLoadout)
        {
            Assert.True(skills.Contains(skillId), $"Missing Wulfric skill {skillId}");
        }

        foreach (var passiveId in BattleFactory.WulfricAlwaysOnPassiveIds)
        {
            Assert.True(passives.Contains(passiveId), $"Missing Wulfric kit passive {passiveId}");
        }

        for (var treeIndex = 1; treeIndex <= 3; treeIndex++)
        {
            for (var tierIndex = 1; tierIndex <= 3; tierIndex++)
            {
                for (var passiveIndex = 1; passiveIndex <= 3; passiveIndex++)
                {
                    var treePassiveId = $"wulfric_tree{treeIndex}_tier{tierIndex}_passive{passiveIndex}";
                    Assert.True(passives.Contains(treePassiveId), $"Missing {treePassiveId}");
                    Assert.True(SkillTreeLookup.TryFindNode(wulfricTrees!, treePassiveId, out _, out _));
                }

                var treeActiveId = $"wulfric_tree{treeIndex}_tier{tierIndex}_active";
                Assert.True(SkillTreeLookup.TryFindNode(wulfricTrees!, treeActiveId, out _, out _));
            }
        }
    }

    [Fact]
    public void InnateSwordCleave_DealsCatalogDamageOnHit()
    {
        var skills = SampleCombatData.CreateSkills();
        var swordCleave = skills.First(skill => skill.Id == "wulfric_innate_active1");
        Assert.Equal(SkillTargetKind.OneEnemy, swordCleave.TargetKind);
        Assert.Equal(CatalogBasicDamageMinimum, swordCleave.BaseDamage.Min);
        Assert.Equal(CatalogBasicDamageMaximum, swordCleave.BaseDamage.Max);

        var battle = CreateWulfricBattle(skills, allySkillIds: [swordCleave.Id]);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];
        var hpBefore = target.Health.CurrentHp;
        ResolveSkillWithoutCrit(battle, swordCleave, actor, target);

        var damageDealt = hpBefore - target.Health.CurrentHp;
        Assert.InRange(damageDealt, CatalogBasicDamageMinimum, CatalogBasicDamageMaximum);
    }

    [Fact]
    public void InnateTaunt_AppliesTauntAndControlledInstability()
    {
        var skills = SampleCombatData.CreateSkills();
        var tauntSkill = skills.First(skill => skill.Id == "wulfric_innate_active2");
        var battle = CreateWulfricBattle(skills, allySkillIds: [tauntSkill.Id]);
        var actor = battle.Allies[0];
        ResolveSkillWithoutCrit(battle, tauntSkill, actor, actor);

        Assert.Equal(1, actor.Tokens.GetStacks(TokenType.Taunt));
        Assert.Equal(2, actor.Tokens.GetStacks(TokenType.ControlledInstability));
    }

    [Fact]
    public void UnleashInstability_ExplodesDestabilizationOnAllOtherLivingCombatants()
    {
        var skills = SampleCombatData.CreateSkills();
        var unleash = skills.First(skill => skill.Id == "wulfric_tree2_tier3_active");
        var battle = CreateWulfricBattle(skills, enemyCount: 2, allySkillIds: [unleash.Id]);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var explodingEnemy = battle.Enemies[0];
        var otherEnemy = battle.Enemies[1];
        explodingEnemy.Tokens.Add(TokenType.Destabilization, 2);
        var explodingHpBefore = explodingEnemy.Health.CurrentHp;
        var otherEnemyHpBefore = otherEnemy.Health.CurrentHp;
        var allyHpBefore = actor.Health.CurrentHp;

        ResolveSkillWithoutCrit(battle, unleash, actor, explodingEnemy);

        Assert.Equal(0, explodingEnemy.Tokens.GetStacks(TokenType.Destabilization));
        Assert.Equal(explodingHpBefore, explodingEnemy.Health.CurrentHp);
        Assert.Equal(6, otherEnemyHpBefore - otherEnemy.Health.CurrentHp);
        Assert.Equal(6, allyHpBefore - actor.Health.CurrentHp);
        Assert.False(explodingEnemy.Health.IsDead);
    }

    [Fact]
    public void LeaderHealOnKill_AppliesOnlyWhenPartyRoleIsLeader()
    {
        var skills = SampleCombatData.CreateSkills();
        var smack = CreateFixedDamageSkill("phase_d_leader_smack", 40);
        var passives = LoadWulfricPassives("wulfric_leader_passive1");
        var battle = CreateWulfricBattle(
            skills.Concat([smack]).ToList(),
            allyCount: 2,
            enemyCount: 2,
            allySkillIds: [smack.Id],
            passivesById: passives,
            unlockPassiveIds: ["wulfric_leader_passive1"]);
        NeutralizeCombatantElements(battle);
        var leader = battle.Allies[0];
        var companion = battle.Allies[1];
        Assert.Equal(CombatantPartyRole.Leader, leader.PartyRole);
        Assert.Equal(CombatantPartyRole.Companion, companion.PartyRole);
        leader.Health.CurrentHp = 10;
        companion.Health.CurrentHp = 10;

        ResolveSkillWithoutCrit(battle, smack, leader, battle.Enemies[0]);
        Assert.Equal(15, leader.Health.CurrentHp);

        battle.Enemies[1].Health.CurrentHp = 20;
        ResolveSkillWithoutCrit(battle, smack, companion, battle.Enemies[1]);
        Assert.Equal(10, companion.Health.CurrentHp);
    }

    [Fact]
    public void CompanionDefensePerMissingHitPoints_AppliesOnlyWhenPartyRoleIsCompanion()
    {
        var skills = SampleCombatData.CreateSkills();
        var smack = CreateFixedDamageSkill("phase_d_companion_smack", 10);
        var passives = LoadWulfricPassives("wulfric_companion_passive1");
        var battle = CreateWulfricBattle(
            [smack],
            allyCount: 2,
            allySkillIds: [smack.Id],
            passivesById: passives,
            unlockPassiveIds: ["wulfric_companion_passive1"]);
        var leader = battle.Allies[0];
        var companion = battle.Allies[1];
        SetHealth(leader, currentHp: 82, maxHp: 100);
        SetHealth(companion, currentHp: 82, maxHp: 100);

        var leaderModifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(battle, leader, battle.Enemies[0], smack);
        var companionModifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(battle, companion, battle.Enemies[0], smack);

        Assert.Equal(0, leaderModifiers.DefenseChanceAdditive);
        Assert.Equal(CompanionDefensePerMissingChunk, companionModifiers.DefenseChanceAdditive, 5);
        Assert.Equal(CompanionMissingHitPointsPerDefenseChunk, 100 - 82);
    }

    [Fact]
    public void CorruptionTier1_AppliesInitialDefensePenaltyAndOnHitBonusUntilNextTurn()
    {
        var skills = SampleCombatData.CreateSkills();
        var smack = CreateFixedDamageSkill("phase_d_corruption_smack", 10);
        var passives = LoadWulfricPassives("wulfric_corruption_tier1_passive1");
        var battle = CreateWulfricBattle(
            [smack],
            allySkillIds: [smack.Id],
            enemySkillIds: [smack.Id],
            passivesById: passives,
            unlockPassiveIds: ["wulfric_corruption_tier1_passive1"],
            corruptionValue: CorruptionRules.Tier0UpperInclusive + 1);
        NeutralizeCombatantElements(battle);
        var wulfric = battle.Allies[0];
        var enemy = battle.Enemies[0];
        enemy.SkillLoadout.Skills.Clear();
        enemy.SkillLoadout.Skills.Add(smack.Id);

        var modifiersBeforeHit = PassiveDataDrivenEngine.GetPermanentStatModifiers(battle, wulfric, enemy, smack);
        Assert.Equal(CorruptionTier1InitialDefensePenalty, modifiersBeforeHit.DefenseChanceAdditive, 5);

        ResolveSkillWithoutCrit(battle, smack, enemy, wulfric);
        var modifiersAfterHit = PassiveDataDrivenEngine.GetPermanentStatModifiers(battle, wulfric, enemy, smack);
        Assert.Equal(
            CorruptionTier1InitialDefensePenalty + CorruptionTier1OnHitDefenseBonus,
            modifiersAfterHit.DefenseChanceAdditive,
            5);

        battle.PassiveBus.RaiseTurnStarted(battle, wulfric, onTokenGranted: null);
        var modifiersAfterOwnTurnStart = PassiveDataDrivenEngine.GetPermanentStatModifiers(battle, wulfric, enemy, smack);
        Assert.Equal(CorruptionTier1InitialDefensePenalty, modifiersAfterOwnTurnStart.DefenseChanceAdditive, 5);
    }

    [Fact]
    public void TreePassive_BasicHitVsDestabilization_GrantsControlledInstability()
    {
        var skills = SampleCombatData.CreateSkills();
        var swordCleave = skills.First(skill => skill.Id == "wulfric_innate_active1");
        var passives = LoadWulfricPassives("wulfric_tree1_tier1_passive1");
        var battle = CreateWulfricBattle(
            skills,
            allySkillIds: [swordCleave.Id],
            passivesById: passives,
            unlockPassiveIds: ["wulfric_tree1_tier1_passive1"]);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];
        target.Tokens.Add(TokenType.Destabilization, 1);

        ResolveSkillWithoutCrit(battle, swordCleave, actor, target);

        Assert.Equal(1, actor.Tokens.GetStacks(TokenType.ControlledInstability));
    }

    private static Dictionary<string, PassiveDefinition> LoadWulfricPassives(params string[] passiveIds)
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

    private static BattleState CreateWulfricBattle(
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

    private static SkillDefinition CreateFixedDamageSkill(string skillId, int damage) =>
        new()
        {
            Id = skillId,
            Name = skillId,
            Element = ElementType.None,
            Type = "Active",
            BaseDamage = new DamageRange { Min = damage, Max = damage },
            BaseCritChance = 0,
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
}
