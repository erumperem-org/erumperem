using System.Globalization;
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

public sealed class PhaseFMariaKitTests
{
    private const int CatalogSoundStrikeDamageMinimum = 4;
    private const int CatalogSoundStrikeDamageMaximum = 8;
    private const int HealingVoiceMinimum = 5;
    private const int HealingVoiceMaximum = 10;
    private const int LeaderExtraDebuffStacks = 1;
    private const int CatalogScreamVulnerabilityStacks = 2;
    private const double CorruptionTier1TokenEfficiency = 0.25;
    private const double CorruptionTier1BasicProcChance = 0.05;
    private const double HealingVoiceEffectivenessBonus = 0.5;
    private const int MariaAllyMaxHitPoints = 70;
    private const double MariaAllyDefenseChance = 0.12;
    private const double MariaAllyCritChance = 0.01;

    [Fact]
    public void Catalog_ContainsAllMariaKitIds()
    {
        var skills = SampleCombatData.CreateSkills().Select(skill => skill.Id).ToHashSet(StringComparer.Ordinal);
        var passives = SampleCombatData.CreatePassives().Select(passive => passive.Id).ToHashSet(StringComparer.Ordinal);
        var trees = CombatDataLoader.LoadSkillTrees(CombatDataLoader.ResolveDefaultSkillTreesPath());
        var mariaTrees = SkillTreeLookup.FindCharacterTrees(trees, "maria");
        Assert.NotNull(mariaTrees);

        foreach (var skillId in BattleFactory.MariaFullSkillLoadout)
        {
            Assert.True(skills.Contains(skillId), $"Missing Maria skill {skillId}");
        }

        foreach (var passiveId in BattleFactory.MariaAlwaysOnPassiveIds)
        {
            Assert.True(passives.Contains(passiveId), $"Missing Maria kit passive {passiveId}");
        }

        for (var treeIndex = 1; treeIndex <= 3; treeIndex++)
        {
            for (var tierIndex = 1; tierIndex <= 3; tierIndex++)
            {
                for (var passiveIndex = 1; passiveIndex <= 3; passiveIndex++)
                {
                    var treePassiveId = $"maria_tree{treeIndex}_tier{tierIndex}_passive{passiveIndex}";
                    Assert.True(passives.Contains(treePassiveId), $"Missing {treePassiveId}");
                    Assert.True(SkillTreeLookup.TryFindNode(mariaTrees!, treePassiveId, out _, out _));
                }

                var treeActiveId = $"maria_tree{treeIndex}_tier{tierIndex}_active";
                Assert.True(SkillTreeLookup.TryFindNode(mariaTrees!, treeActiveId, out _, out _));
            }
        }
    }

    [Fact]
    public void HealingVoice_HealsWhenCombatHealingUnlocked()
    {
        Assert.True(CombatHealUnlock.IsCombatHealingUnlocked);
        var skills = SampleCombatData.CreateSkills();
        var healingVoice = skills.First(skill => skill.Id == "maria_innate_active2");
        Assert.Equal(SkillTargetKind.SelfOrAlly, healingVoice.TargetKind);

        var battle = CreateMariaBattle(skills, allySkillIds: [healingVoice.Id]);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        actor.Health.CurrentHp = 10;
        var hpBefore = actor.Health.CurrentHp;
        ResolveSkillWithoutCrit(battle, healingVoice, actor, actor);

        Assert.True(actor.Health.CurrentHp > hpBefore);
        Assert.InRange(actor.Health.CurrentHp - hpBefore, HealingVoiceMinimum, HealingVoiceMaximum);
    }

    [Fact]
    public void Leader_AppliesOneExtraDebuffStack_OnlyWhenPartyRoleIsLeader()
    {
        var skills = SampleCombatData.CreateSkills();
        var amplifiedScream = skills.First(skill => skill.Id == "maria_innate_active3");
        var passives = LoadMariaPassives("maria_leader_passive1");
        var battle = CreateMariaBattle(
            skills,
            allyCount: 2,
            allySkillIds: [amplifiedScream.Id],
            passivesById: passives,
            unlockPassiveIds: ["maria_leader_passive1"]);
        NeutralizeCombatantElements(battle);
        var leader = battle.Allies[0];
        var companion = battle.Allies[1];
        Assert.Equal(CombatantPartyRole.Leader, leader.PartyRole);
        Assert.Equal(CombatantPartyRole.Companion, companion.PartyRole);

        ResolveSkillWithoutCrit(battle, amplifiedScream, leader, battle.Enemies[0]);
        Assert.Equal(
            CatalogScreamVulnerabilityStacks + LeaderExtraDebuffStacks,
            battle.Enemies[0].Tokens.GetStacks(TokenType.Vulnerability));

        foreach (var enemy in battle.Enemies)
        {
            enemy.Tokens = new TokenComponent();
        }

        ResolveSkillWithoutCrit(battle, amplifiedScream, companion, battle.Enemies[0]);
        Assert.Equal(CatalogScreamVulnerabilityStacks, battle.Enemies[0].Tokens.GetStacks(TokenType.Vulnerability));
    }

    [Fact]
    public void Companion_AppliesOneExtraBuffStack_OnlyWhenPartyRoleIsCompanion()
    {
        var skills = SampleCombatData.CreateSkills();
        var inspirationalSong = skills.First(skill => skill.Id == "maria_innate_active4");
        var passives = LoadMariaPassives("maria_companion_passive1");
        var battle = CreateMariaBattle(
            skills,
            allyCount: 2,
            allySkillIds: [inspirationalSong.Id],
            passivesById: passives,
            unlockPassiveIds: ["maria_companion_passive1"]);
        NeutralizeCombatantElements(battle);
        var leader = battle.Allies[0];
        var companion = battle.Allies[1];
        Assert.Equal(CombatantPartyRole.Leader, leader.PartyRole);
        Assert.Equal(CombatantPartyRole.Companion, companion.PartyRole);

        companion.Tokens.Add(TokenType.BonusAction, 1);
        ResolveSkillWithoutCrit(battle, inspirationalSong, companion, companion);
        Assert.Equal(4, companion.Tokens.GetStacks(TokenType.Strength));
        Assert.Equal(4, leader.Tokens.GetStacks(TokenType.Strength));

        leader.Tokens = new TokenComponent();
        companion.Tokens = new TokenComponent();
        leader.Tokens.Add(TokenType.BonusAction, 1);
        ResolveSkillWithoutCrit(battle, inspirationalSong, leader, leader);
        Assert.Equal(3, leader.Tokens.GetStacks(TokenType.Strength));
        Assert.Equal(3, companion.Tokens.GetStacks(TokenType.Strength));
    }

    [Fact]
    public void CorruptionTier1_HasTokenEfficiencyAndBasicProcChance()
    {
        var passives = LoadMariaPassives("maria_corruption_tier1_passive1");
        var corruptionPassive = passives["maria_corruption_tier1_passive1"];
        Assert.Equal(1, corruptionPassive.CorruptionMinTier);
        Assert.Contains(
            corruptionPassive.Effects,
            effect =>
                effect.Operation == PassiveEffectOperationKind.TokenStatChange &&
                effect.Magnitude == CorruptionTier1TokenEfficiency);
        Assert.Contains(
            corruptionPassive.Effects,
            effect =>
                effect.Operation == PassiveEffectOperationKind.CastSkill &&
                effect.SkillId == "maria_innate_active1" &&
                effect.ChanceToTrigger == CorruptionTier1BasicProcChance);

        var smack = CreateFixedDamageSkill("phase_f_corruption_smack", 10);
        var battle = CreateMariaBattle(
            [smack],
            allySkillIds: [smack.Id],
            passivesById: passives,
            unlockPassiveIds: ["maria_corruption_tier1_passive1"],
            corruptionValue: CorruptionRules.Tier0UpperInclusive + 1);
        var maria = battle.Allies[0];
        Assert.Equal(
            1.0 + CorruptionTier1TokenEfficiency,
            PassiveDataDrivenEngine.GetTokenEfficiencyMultiplier(battle, maria, TokenType.Strength),
            5);
    }

    [Fact]
    public void ResurrectionHymn_CanTargetDeadAllies()
    {
        var resurrection = SampleCombatData.CreateSkills().First(skill => skill.Id == "maria_tree1_tier3_active");
        Assert.True(resurrection.CanTargetDeadAllies);
        Assert.Equal(SkillTargetKind.SelfAndAlly, resurrection.TargetKind);
    }

    [Fact]
    public void TreePassive_HealingVoiceIsFiftyPercentMoreEffective()
    {
        var skills = SampleCombatData.CreateSkills();
        var healingVoice = skills.First(skill => skill.Id == "maria_innate_active2");
        var passives = LoadMariaPassives("maria_tree1_tier1_passive1");
        var battle = CreateMariaBattle(
            skills,
            allySkillIds: [healingVoice.Id],
            passivesById: passives,
            unlockPassiveIds: ["maria_tree1_tier1_passive1"]);
        var modifiers = PassiveDataDrivenEngine.GetPermanentStatModifiers(
            battle,
            battle.Allies[0],
            battle.Enemies[0],
            healingVoice);
        Assert.Equal(HealingVoiceEffectivenessBonus, modifiers.SkillHealEffectivenessAdditive, 5);
    }

    [Fact]
    public void MariaAllyStatDefinition_MatchesGddBaseStats()
    {
        var catalogDirectory = CombatDataLoader.ResolveDefaultCatalogDirectory();
        var repositoryRoot = Path.GetFullPath(Path.Combine(catalogDirectory, "..", "..", ".."));
        var mariaStatDefinitionPath = Path.Combine(
            repositoryRoot,
            "Assets",
            "_Project",
            "ScriptableObjects",
            "Characters",
            "MariaAllyCharacterStatDefinition.asset");
        Assert.True(File.Exists(mariaStatDefinitionPath), mariaStatDefinitionPath);
        var assetText = File.ReadAllText(mariaStatDefinitionPath);
        Assert.Contains("characterId: Maria", assetText, StringComparison.Ordinal);
        Assert.Contains("displayName: The Star", assetText, StringComparison.Ordinal);
        Assert.Contains("progressionCharacterId: maria", assetText, StringComparison.Ordinal);
        Assert.Contains($"maxHitPoints: {MariaAllyMaxHitPoints}", assetText, StringComparison.Ordinal);
        Assert.Contains(
            $"defenseChance: {MariaAllyDefenseChance.ToString(CultureInfo.InvariantCulture)}",
            assetText,
            StringComparison.Ordinal);
        Assert.Contains(
            $"critChance: {MariaAllyCritChance.ToString(CultureInfo.InvariantCulture)}",
            assetText,
            StringComparison.Ordinal);
    }

    [Fact]
    public void InnateSoundStrike_DealsCatalogDamageOnHit()
    {
        var skills = SampleCombatData.CreateSkills();
        var soundStrike = skills.First(skill => skill.Id == "maria_innate_active1");
        Assert.Equal(SkillTargetKind.OneEnemy, soundStrike.TargetKind);
        Assert.Equal(CatalogSoundStrikeDamageMinimum, soundStrike.BaseDamage.Min);
        Assert.Equal(CatalogSoundStrikeDamageMaximum, soundStrike.BaseDamage.Max);

        var battle = CreateMariaBattle(skills, allySkillIds: [soundStrike.Id]);
        NeutralizeCombatantElements(battle);
        var actor = battle.Allies[0];
        var target = battle.Enemies[0];
        var hpBefore = target.Health.CurrentHp;
        ResolveSkillWithoutCrit(battle, soundStrike, actor, target);
        Assert.InRange(hpBefore - target.Health.CurrentHp, CatalogSoundStrikeDamageMinimum, CatalogSoundStrikeDamageMaximum);
    }

    private static Dictionary<string, PassiveDefinition> LoadMariaPassives(params string[] passiveIds)
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

    private static BattleState CreateMariaBattle(
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
