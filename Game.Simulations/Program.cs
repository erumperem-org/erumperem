using Game.Core.Abstractions;
using Game.Core.Analytics;
using Game.Core.Data;
using Game.Core.Engine;
using Game.Core.Models;
using Game.Simulations;

var parsed = ArgsParser.Parse(args);
if (parsed.ShowHelp)
{
    PrintHelp();
    return;
}

var baseSeed = parsed.SeedProvided ? parsed.Seed : Random.Shared.Next();
if (!parsed.SeedProvided)
{
    Console.WriteLine($"Random base seed: {baseSeed}  (re-run with --seed {baseSeed} for identical results)");
    Console.WriteLine();
}

Directory.CreateDirectory(parsed.OutputDirectory);

var skills = string.IsNullOrWhiteSpace(parsed.SkillsPath)
    ? CombatDataLoader.LoadSkills(CombatDataLoader.ResolveDefaultSkillsPath())
    : CombatDataLoader.LoadSkills(parsed.SkillsPath);

var passivesFile = string.IsNullOrWhiteSpace(parsed.PassivesPath)
    ? CombatDataLoader.ResolveDefaultPassivesPath()
    : parsed.PassivesPath;
var passivesById = CombatDataLoader.LoadPassives(passivesFile)
    .ToDictionary(passiveDefinition => passiveDefinition.Id, passiveDefinition => passiveDefinition);

var skillTreesPath = string.IsNullOrWhiteSpace(parsed.SkillTreesPath)
    ? CombatDataLoader.ResolveDefaultSkillTreesPath()
    : parsed.SkillTreesPath;
var skillTreesRoots = CombatDataLoader.LoadSkillTrees(skillTreesPath);

if (!string.IsNullOrWhiteSpace(parsed.EnemiesPath))
{
    _ = CombatDataLoader.LoadEnemies(parsed.EnemiesPath);
}

var allEvents = new List<CombatEvent>();
for (var i = 0; i < parsed.Battles; i++)
{
    var seed = baseSeed + i;
    var random = new SeededRandomSource(seed);
    var collector = new CombatEventCollector();
    var simulator = new BattleSimulator(random, collector);
    var battle = BuildBattle(parsed, skills, passivesById, skillTreesRoots, random);
    simulator.Simulate(battle, maxTurns: 100);
    allEvents.AddRange(collector.Events);
}

var eventsCsv = CombatAnalyticsExporter.BuildEventsCsv(allEvents);
var aggregates = CombatAnalyticsExporter.BuildAggregates(allEvents);
var aggregatesCsv = CombatAnalyticsExporter.BuildAggregatesCsv(aggregates);
var passiveAgg = CombatAnalyticsExporter.BuildPassiveAggregates(allEvents, passivesById.Keys);
var passiveCsv = CombatAnalyticsExporter.BuildPassiveAggregatesCsv(passiveAgg);

var eventsPath = Path.Combine(parsed.OutputDirectory, "combat_events.csv");
var aggregatesPath = Path.Combine(parsed.OutputDirectory, "combat_aggregates.csv");
var passivePath = Path.Combine(parsed.OutputDirectory, "passive_aggregates.csv");
File.WriteAllText(eventsPath, eventsCsv);
File.WriteAllText(aggregatesPath, aggregatesCsv);
File.WriteAllText(passivePath, passiveCsv);

Console.WriteLine($"Simulations: {parsed.Battles}  (seed base: {baseSeed}{(parsed.SeedProvided ? ", fixed" : ", random")})");
Console.WriteLine($"Preset: {DescribePreset(parsed)}");
Console.WriteLine($"Events CSV: {eventsPath}");
Console.WriteLine($"Skill aggregates CSV: {aggregatesPath}");
Console.WriteLine($"Passive aggregates CSV: {passivePath}");
Console.WriteLine();
Console.WriteLine("Skill win rates (Allies):");
foreach (var row in aggregates.OrderBy(aggregateRow => aggregateRow.EntityId))
{
    Console.WriteLine($"  {row.EntityId}: win_rate={row.WinRate:0.###} ({row.Wins}/{row.Matches} matches)");
}

Console.WriteLine();
Console.WriteLine("Passive win rates (battles where passive was unlocked on at least one ally):");
foreach (var row in passiveAgg.OrderBy(passiveRow => passiveRow.PassiveId))
{
    Console.WriteLine(
        $"  {row.PassiveId}: win_rate={row.WinRate:0.###} ({row.Wins}/{row.BattlesWithPassive} battles), presence={row.PresenceRate:0.###}");
}

static string DescribePreset(ParsedArgs parsedArgs) => parsedArgs.Preset switch
{
    SimulationUnlockPreset.Random => $"random tree unlocks (0..{parsedArgs.MaxPointsBudget} points)",
    SimulationUnlockPreset.SingleTreeTier => $"tree {parsedArgs.TreeIndex} through tier {parsedArgs.TierCap}",
    SimulationUnlockPreset.AllTreesTier => $"all trees tier 1..{parsedArgs.AllTreesTierCap}",
    SimulationUnlockPreset.FullPassivesCatalog => "all passives from passives.json (stress)",
    _ => parsedArgs.Preset.ToString(),
};

static BattleState BuildBattle(
    ParsedArgs parsed,
    IReadOnlyList<SkillDefinition> skills,
    IReadOnlyDictionary<string, PassiveDefinition> passivesById,
    IReadOnlyList<CharacterSkillTreesDefinition> skillTreesRoots,
    IRandomSource random)
{
    var battle = BattleFactory.CreateSampleBattle(
        skills,
        allyCount: parsed.AllyCount,
        enemyCount: parsed.EnemyCount,
        corruptionValue: random.Next(0, 101),
        allySkillIds: BattleFactory.WulfricFullSkillLoadout,
        passivesById: passivesById,
        unlockAllPassiveNodesForAllies: false);

    var charTrees = SimulationSkillTreeSetup.GetCharacter(skillTreesRoots);

    switch (parsed.Preset)
    {
        case SimulationUnlockPreset.Random:
            SimulationSkillTreeSetup.ApplyRandomTreeUnlocks(battle.Allies, charTrees, random, parsed.MaxPointsBudget);
            break;
        case SimulationUnlockPreset.SingleTreeTier:
            var singleTreeNodeIds = SimulationSkillTreeSetup.GetNodeIdsForTreeMaxTier(
                charTrees,
                parsed.TreeIndex!.Value,
                parsed.TierCap!.Value);
            SimulationSkillTreeSetup.ApplyNodeUnlocks(battle.Allies, charTrees, singleTreeNodeIds);
            break;
        case SimulationUnlockPreset.AllTreesTier:
            var allTreesNodeIds = SimulationSkillTreeSetup.GetNodeIdsAllTreesMaxTier(charTrees, parsed.AllTreesTierCap!.Value);
            SimulationSkillTreeSetup.ApplyNodeUnlocks(battle.Allies, charTrees, allTreesNodeIds);
            break;
        case SimulationUnlockPreset.FullPassivesCatalog:
            BattleFactory.UnlockAllPassivesFromCatalog(battle, passivesById);
            foreach (var ally in battle.Allies)
            {
                ally.Progression.Level = passivesById.Count;
                ally.Progression.SpentPoints = passivesById.Count;
            }

            break;
    }

    foreach (var ally in battle.Allies)
    {
        ally.Health.CurrentHp = random.Next((int)(ally.Health.MaxHp * 0.5), ally.Health.MaxHp + 1);
    }

    foreach (var enemy in battle.Enemies)
    {
        enemy.Health.CurrentHp = random.Next(10, enemy.Health.MaxHp + 1);
    }

    return battle;
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        Game.Simulations — batch combat runs

        Presets (default: random unlocks, 0..12 skill points per battle):
          --maxPoints N       Cap for random unlock budget (default 12). Hero level = points spent.
                              Tier-3 passives need the full tier 1+2 of that tree first (8 pts); random
                              spreads points across trees, so t3 passives are often absent — use
                              --tree/--tier or --allTreesTier to force them, or see passive_aggregates.csv
                              (all passives listed; zeros = never rolled in sample).
          --tree I --tier T   Single element tree I (1=Fogo, 2=Metal, 3=Anomalia), tiers 1..T unlocked.
          --allTreesTier T    All three trees: tiers 1..T unlocked.
          --fullPassives      Unlock every passive from passives.json (ignores tree; stress test).

        Other:
          --battles N         Number of battles (default 100)
          --seed N            Fixed base RNG seed (reproducible). Omit for a random seed each run.
          --out DIR           Output directory (default SimulationOutput under project)
          --allies N          Ally count (default 2)
          --enemyCount N      Enemy count (default 4)
          --skills PATH       skills.json
          --passives PATH     passives.json
          --skillTrees PATH   skill_trees.json

        Outputs:
          combat_events.csv      All events; BattleStarted includes passive_loadout (comma-separated passive ids).
          combat_aggregates.csv  Win rates per skill (ActionUsed).
          passive_aggregates.csv All passives from passives.json; battles_with_passive=0 if never rolled.

        Examples:
          dotnet run -- --tree 1 --tier 3 --battles 200
          dotnet run -- --allTreesTier 1 --seed 0
        """);
}

internal enum SimulationUnlockPreset
{
    Random,
    SingleTreeTier,
    AllTreesTier,
    FullPassivesCatalog,
}

internal sealed class ParsedArgs
{
    public required int Battles { get; init; }
    public required int Seed { get; init; }
    public required bool SeedProvided { get; init; }
    public required string OutputDirectory { get; init; }
    public required string SkillsPath { get; init; }
    public required string PassivesPath { get; init; }
    public required string EnemiesPath { get; init; }
    public required string SkillTreesPath { get; init; }
    public required int AllyCount { get; init; }
    public required int EnemyCount { get; init; }
    public required int MaxPointsBudget { get; init; }
    public required SimulationUnlockPreset Preset { get; init; }
    public int? TreeIndex { get; init; }
    public int? TierCap { get; init; }
    public int? AllTreesTierCap { get; init; }
    public bool ShowHelp { get; init; }
}

internal static class ArgsParser
{
    public static ParsedArgs Parse(string[] args)
    {
        var battles = 100;
        var seed = 0;
        var seedProvided = false;
        var output = DefaultSimulationOutputDirectory();
        var skillsPath = string.Empty;
        var passivesPath = string.Empty;
        var enemiesPath = string.Empty;
        var skillTreesPath = string.Empty;
        var allyCount = 2;
        var enemyCount = 4;
        var maxPoints = 12;
        var fullPassives = false;
        int? treeIdx = null;
        int? tierCap = null;
        int? allTreesTier = null;
        var showHelp = args.Any(commandLineArg => commandLineArg is "-h" or "-?" or "--help");

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (arg == "--battles" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedBattles))
            {
                battles = parsedBattles;
                i++;
            }
            else if (arg == "--seed" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedSeed))
            {
                seed = parsedSeed;
                seedProvided = true;
                i++;
            }
            else if (arg == "--out" && i + 1 < args.Length)
            {
                output = args[i + 1];
                i++;
            }
            else if (arg == "--skills" && i + 1 < args.Length)
            {
                skillsPath = args[i + 1];
                i++;
            }
            else if (arg == "--passives" && i + 1 < args.Length)
            {
                passivesPath = args[i + 1];
                i++;
            }
            else if (arg == "--enemies" && i + 1 < args.Length)
            {
                enemiesPath = args[i + 1];
                i++;
            }
            else if (arg == "--skillTrees" && i + 1 < args.Length)
            {
                skillTreesPath = args[i + 1];
                i++;
            }
            else if (arg == "--allies" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedAllyCount))
            {
                allyCount = Math.Max(1, parsedAllyCount);
                i++;
            }
            else if (arg == "--enemyCount" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedEnemyCount))
            {
                enemyCount = Math.Max(1, parsedEnemyCount);
                i++;
            }
            else if (arg == "--maxPoints" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedMaxPoints))
            {
                maxPoints = Math.Clamp(parsedMaxPoints, 0, 999);
                i++;
            }
            else if (arg == "--tree" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedTreeIndex))
            {
                treeIdx = parsedTreeIndex;
                i++;
            }
            else if (arg == "--tier" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedTierCap))
            {
                tierCap = parsedTierCap;
                i++;
            }
            else if (arg == "--allTreesTier" && i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedAllTreesTier))
            {
                allTreesTier = parsedAllTreesTier;
                i++;
            }
            else if (arg == "--fullPassives")
            {
                fullPassives = true;
            }
        }

        SimulationUnlockPreset preset;
        if (fullPassives)
        {
            preset = SimulationUnlockPreset.FullPassivesCatalog;
        }
        else if (treeIdx is not null || tierCap is not null)
        {
            if (treeIdx is null || tierCap is null)
            {
                throw new ArgumentException("Use --tree and --tier together (e.g. --tree 1 --tier 3).");
            }

            if (treeIdx is < 1 or > 3 || tierCap < 1)
            {
                throw new ArgumentException("--tree must be 1..3 and --tier at least 1.");
            }

            preset = SimulationUnlockPreset.SingleTreeTier;
        }
        else if (allTreesTier is not null)
        {
            if (allTreesTier < 1)
            {
                throw new ArgumentException("--allTreesTier must be >= 1.");
            }

            preset = SimulationUnlockPreset.AllTreesTier;
        }
        else
        {
            preset = SimulationUnlockPreset.Random;
        }

        return new ParsedArgs
        {
            Battles = Math.Max(1, battles),
            Seed = seed,
            SeedProvided = seedProvided,
            OutputDirectory = output,
            SkillsPath = skillsPath,
            PassivesPath = passivesPath,
            EnemiesPath = enemiesPath,
            SkillTreesPath = skillTreesPath,
            AllyCount = allyCount,
            EnemyCount = enemyCount,
            MaxPointsBudget = maxPoints,
            Preset = preset,
            TreeIndex = treeIdx,
            TierCap = tierCap,
            AllTreesTierCap = allTreesTier,
            ShowHelp = showHelp,
        };
    }

    private static string DefaultSimulationOutputDirectory()
    {
        var projectDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", ".."));
        return Path.Combine(projectDir, "SimulationOutput");
    }
}
