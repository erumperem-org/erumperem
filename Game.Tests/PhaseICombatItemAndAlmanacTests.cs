using Game.Core.Almanac;
using Game.Core.Data;
using Game.Core.Domain;
using Game.Core.Engine;
using Game.Core.Items;
using Game.Core.Models;
using Game.Core.Progression;

namespace Game.Tests;

public sealed class PhaseICombatItemAndAlmanacTests
{
    [Fact]
    public void DefaultCatalog_HasThirtySevenItemsWithExpectedIds()
    {
        var catalog = CombatItemCatalogFactory.CreateDefaultCatalog();
        Assert.Equal(CombatItemCatalogFactory.ExpectedItemCount, catalog.Count);
        Assert.Equal(catalog.Count, catalog.Select(item => item.Id).Distinct(StringComparer.Ordinal).Count());

        Assert.Contains(catalog, item => item.Id == "health_potion_small" && item.Rarity == CombatItemRarity.Common);
        Assert.Contains(catalog, item => item.Id == "paladin_extreme" && item.Rarity == CombatItemRarity.Legendary);
        Assert.Contains(catalog, item => item.Id == "reset_skill_tree_maria" && item.UtilityKind == CombatItemUtilityKind.ResetMariaSkillTree);
        Assert.DoesNotContain(catalog, item => item.Id.Contains("matsuda", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AbsoluteHealthPotionSmall_AddsTenMaxHp()
    {
        var combatant = CreateAllyWithBaseStats(maxHp: 100, defenseChance: 0.25, critChance: 0.03);
        var healthPotionSmall = CombatItemCatalogFactory.CreateDefaultCatalogById()["health_potion_small"];

        CombatItemStatApplier.ApplyItemToCombatant(combatant, healthPotionSmall);

        Assert.Equal(110, combatant.Health.MaxHp);
        Assert.Equal(110, combatant.Health.CurrentHp);
        Assert.Equal(0.25, combatant.Stats.DefenseChance);
        Assert.Equal(0.03, combatant.Stats.CritChance);
    }

    [Fact]
    public void RelativeVitalityCharmSmall_AddsFivePercentMaxHp()
    {
        var combatant = CreateAllyWithBaseStats(maxHp: 100, defenseChance: 0.25, critChance: 0.03);
        var vitalityCharmSmall = CombatItemCatalogFactory.CreateDefaultCatalogById()["vitality_charm_small"];

        CombatItemStatApplier.ApplyItemToCombatant(combatant, vitalityCharmSmall);

        Assert.Equal(105, combatant.Health.MaxHp);
        Assert.Equal(105, combatant.Health.CurrentHp);
    }

    [Fact]
    public void StreamingAssetsItemsJson_MatchesFactoryCatalog()
    {
        var catalogDirectory = CombatDataLoader.ResolveDefaultCatalogDirectory();
        var itemsPath = Path.Combine(catalogDirectory, "items.json");
        Assert.True(File.Exists(itemsPath), itemsPath);

        var fromJson = CombatDataLoader.LoadItems(itemsPath);
        var fromFactory = CombatItemCatalogFactory.CreateDefaultCatalog();
        Assert.Equal(fromFactory.Count, fromJson.Count);

        var jsonById = fromJson.ToDictionary(item => item.Id, StringComparer.Ordinal);
        foreach (var factoryItem in fromFactory)
        {
            Assert.True(jsonById.ContainsKey(factoryItem.Id), factoryItem.Id);
            var jsonItem = jsonById[factoryItem.Id];
            Assert.Equal(factoryItem.DisplayName, jsonItem.DisplayName);
            Assert.Equal(factoryItem.Rarity, jsonItem.Rarity);
            Assert.Equal(factoryItem.Kind, jsonItem.Kind);
            Assert.Equal(factoryItem.IconFamilyId, jsonItem.IconFamilyId);
            Assert.Equal(factoryItem.UtilityKind, jsonItem.UtilityKind);
            Assert.Equal(factoryItem.StatModifiers.Count, jsonItem.StatModifiers.Count);
            for (var modifierIndex = 0; modifierIndex < factoryItem.StatModifiers.Count; modifierIndex++)
            {
                Assert.Equal(factoryItem.StatModifiers[modifierIndex].Stat, jsonItem.StatModifiers[modifierIndex].Stat);
                Assert.Equal(factoryItem.StatModifiers[modifierIndex].Scale, jsonItem.StatModifiers[modifierIndex].Scale);
                Assert.Equal(factoryItem.StatModifiers[modifierIndex].IntensityRank, jsonItem.StatModifiers[modifierIndex].IntensityRank);
            }
        }
    }

    [Fact]
    public void MariaTreeReset_TargetsCharacterIdMaria_NotMatsuda()
    {
        var resetMariaItem = CombatItemCatalogFactory.CreateDefaultCatalogById()["reset_skill_tree_maria"];
        Assert.Equal(CombatItemUtilityKind.ResetMariaSkillTree, resetMariaItem.UtilityKind);
        Assert.Equal(["maria"], SkillTreeResetRules.ResolveCharacterIdsToReset(resetMariaItem.UtilityKind));
        Assert.DoesNotContain(
            SkillTreeResetRules.ResolveCharacterIdsToReset(resetMariaItem.UtilityKind),
            characterId => characterId.Contains("matsuda", StringComparison.OrdinalIgnoreCase));

        var unlockedNodes = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase)
        {
            ["wulfric"] = new Dictionary<string, bool> { ["wulfric_tree1_tier1_active"] = true },
            ["buck"] = new Dictionary<string, bool> { ["buck_tree1_tier1_active"] = true },
            ["maria"] = new Dictionary<string, bool> { ["maria_tree1_tier1_active"] = true },
            ["matsuda"] = new Dictionary<string, bool> { ["should_not_be_a_combat_kit"] = true },
        };

        var afterReset = SkillTreeResetRules.ResetUnlockedNodes(unlockedNodes, resetMariaItem.UtilityKind);
        Assert.False(afterReset.ContainsKey("maria"));
        Assert.True(afterReset["wulfric"]["wulfric_tree1_tier1_active"]);
        Assert.True(afterReset["buck"]["buck_tree1_tier1_active"]);
        Assert.True(afterReset["matsuda"]["should_not_be_a_combat_kit"]);
    }

    [Fact]
    public void Almanac_HidesUnusedEnemySkill_UntilRecordedAsUsed()
    {
        var enemies = CombatDataLoader.LoadEnemies(CombatDataLoader.ResolveDefaultEnemiesPath());
        var horseBoss = Assert.Single(enemies, enemy => enemy.Id == "horse_boss");
        var skillsById = CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath())
            .ToDictionary(skill => skill.Id, StringComparer.Ordinal);
        var passivesById = CombatDataLoader.LoadPassives(CombatDataLoader.ResolveDefaultPassivesPath())
            .ToDictionary(passive => passive.Id, StringComparer.Ordinal);
        var progress = new EnemyAlmanacProgress();

        var hiddenEntry = EnemyAlmanacEntryBuilder.Build(
            horseBoss.Id,
            progress,
            horseBoss,
            liveCombatant: null,
            skillsById,
            passivesById);

        var hiddenBite = Assert.Single(hiddenEntry.ActiveSkills, skill => skill.SkillId == "horse_boss_painful_bite");
        Assert.False(hiddenBite.IsRevealed);
        Assert.Equal(EnemyAlmanacEntry.HiddenSkillDisplayName, hiddenBite.DisplayName);
        Assert.Contains(hiddenEntry.PassiveSummaries, summary => summary.Contains("horse_boss_summon_fairy_on_hp_tier"));

        progress.RecordActiveSkillUsed(horseBoss.Id, "horse_boss_painful_bite");
        var revealedEntry = EnemyAlmanacEntryBuilder.Build(
            horseBoss.Id,
            progress,
            horseBoss,
            liveCombatant: null,
            skillsById,
            passivesById);
        var revealedBite = Assert.Single(revealedEntry.ActiveSkills, skill => skill.SkillId == "horse_boss_painful_bite");
        Assert.True(revealedBite.IsRevealed);
        Assert.Contains("Painful Bite", revealedBite.FullDescription, StringComparison.OrdinalIgnoreCase);

        var stillHiddenClaws = Assert.Single(revealedEntry.ActiveSkills, skill => skill.SkillId == "horse_boss_sharp_claws");
        Assert.False(stillHiddenClaws.IsRevealed);
    }

    [Fact]
    public void BattleSimulator_RecordsEnemyAlmanacWhenArchetypeUsesSkill()
    {
        var skills = CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath());
        var bite = skills.First(skill => skill.Id == "horse_boss_painful_bite");
        var battle = BattleFactory.CreateSampleBattle(
            [bite],
            allyCount: 1,
            enemyCount: 1,
            allySkillIds: [bite.Id],
            enemySkillIds: [bite.Id]);
        EnemyCatalogIdentity.AssignArchetypeTag(battle.Enemies[0], "horse_boss");
        var simulator = new BattleSimulator(new Game.Core.Abstractions.SeededRandomSource(1), new Game.Core.Analytics.CombatEventCollector());

        simulator.ResolveChosenAction(
            battle,
            new ChosenAction
            {
                Actor = battle.Enemies[0],
                Target = battle.Allies[0],
                Skill = bite,
                ActionType = ActionType.Skill,
            });

        Assert.True(battle.EnemyAlmanac.HasRevealedActiveSkill("horse_boss", "horse_boss_painful_bite"));
        Assert.False(battle.EnemyAlmanac.HasRevealedActiveSkill("horse_boss", "horse_boss_sharp_claws"));
    }

    private static Combatant CreateAllyWithBaseStats(int maxHp, double defenseChance, double critChance)
    {
        var battle = BattleFactory.CreateSampleBattle(
            [
                new SkillDefinition
                {
                    Id = "phase_i_probe",
                    Name = "phase_i_probe",
                    Element = ElementType.None,
                    Type = "Active",
                    BaseDamage = new DamageRange { Min = 1, Max = 1 },
                    BaseCritChance = 0,
                    Accuracy = 1,
                    TargetKind = SkillTargetKind.OneEnemy,
                },
            ],
            allyCount: 1,
            enemyCount: 1,
            allySkillIds: ["phase_i_probe"]);
        var ally = battle.Allies[0];
        ally.Health = new HealthComponent
        {
            CurrentHp = maxHp,
            MaxHp = maxHp,
            IsDead = false,
            IsDeathblowPending = false,
        };
        ally.Stats = new StatsComponent
        {
            Speed = 6,
            Accuracy = 1.0,
            CritChance = critChance,
            DefenseChance = defenseChance,
        };
        return ally;
    }
}
