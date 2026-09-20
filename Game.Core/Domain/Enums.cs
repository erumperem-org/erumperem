namespace Game.Core.Domain;

public enum Side
{
    Allies = 0,
    Enemies = 1,
}

public enum Faction
{
    Player = 0,
    Enemy = 1,
    Corpse = 2,
}

public enum ElementType
{
    None = 0,
    Fire = 1,
    Metal = 2,
    Anomaly = 3,
}

public enum TokenType
{
    Taunt = 4,
    Stealth = 5,
    Stun = 7,
    ControlledInstability = 8,
    Destabilization = 9,
    Strength = 10,
    Defense = 11,
    Weaken = 12,
    Vulnerability = 13,
    Confusion = 14,
    Bleeding = 15,
    LuckyShot = 16,
    Dexterity = 17,
    Exposition = 18,
    Corrosion = 19,
    Mark = 20,
    Regeneration = 21,
    Clumsy = 22,
    BonusAction = 23,
    Hypnosis = 24,
    Dizzy = 25,
    Burn = 26,
    PermaStrength = 27,
    PermaDefense = 28,
    PermaWeaken = 29,
    PermaVulnerability = 30,
    PermaDexterity = 31,
    PermaClumsy = 32,
    PermaExposition = 33,
    PermaStealth = 34,
}

public enum DotType
{
    Burn = 0,
    Blight = 1,
    Bleed = 2,
}

public enum EffectType
{
    ApplyToken = 0,
    ApplyDot = 1,
    Push = 2,
    Pull = 3,
    HealHp = 4,
    ApplyStun = 5,
    HealHpPercent = 6,
    ApplyRandomDot = 7,
    RemoveAllDebuffTokens = 8,
    ConsumeAllTokenStacksDealDamagePerStack = 9,
    ConsumeAllTokenStacksHealPerStack = 10,
    SelfDamageFlat = 11,
    TriggerDestabilizationOnTargets = 12,
    ApplyBonusAction = 13,
}

/// <summary>
/// Who the player/AI selects and who receives primary hit/damage.
/// Integer values 0–2 keep Unity ScriptableObjects compatible with the old Enemy/Ally/Self assets.
/// </summary>
public enum SkillTargetKind
{
    OneEnemy = 0,
    OneAlly = 1,
    Self = 2,
    UpToThreeEnemies = 3,
    AllEnemies = 4,
    SelfOrAlly = 5,
    SelfAndAlly = 6,
}

/// <summary>
/// Who receives a given on-hit effect relative to the hit.
/// Distinct from <see cref="SkillTargetKind"/> (selection / primary damage).
/// </summary>
public enum EffectScope
{
    Default = 0,
    Self = 1,
    AllAllies = 2,
    AllEnemies = 3,
}

public enum ActionType
{
    Skill = 0,
    CombatItem = 1,
}

public enum BattleEventType
{
    BattleStarted = 0,
    TurnStarted = 1,
    DotTick = 2,
    ActionUsed = 3,
    HitResolved = 4,
    DamageApplied = 5,
    TokenApplied = 6,
    CombatantDied = 7,
    BattleEnded = 8,

    /// <summary>World corruption changed (skill use, effects, heals).</summary>
    CorruptionAdjusted = 9,

    /// <summary>DoT aplicado pela resolução de uma skill (ou passiva) — ver <see cref="CombatEvent.DotType"/> / duração.</summary>
    DotInflicted = 10,

    /// <summary>Feed de passiva para narrativa/UI (dano modificado, cura extra, etc.).</summary>
    PassiveCombatNarrative = 11,

    /// <summary>Inimigo invocado em slot livre (ex.: passiva do Horse Boss).</summary>
    CombatantSpawned = 12,
}

/// <summary>
/// Combat party slot. Overworld Main maps to Leader; Companion stays Companion; enemies and summons are None.
/// </summary>
public enum CombatantPartyRole
{
    None = 0,
    Leader = 1,
    Companion = 2,
}

/// <summary>Which party slot a passive is allowed to run on. Any = no role gate.</summary>
public enum PassiveRequiredPartyRole
{
    Any = 0,
    Leader = 1,
    Companion = 2,
}

/// <summary>Authoring toggle on a combat ability asset: one ScriptableObject is either an active skill or a passive.</summary>
public enum CombatAbilityKind
{
    Active = 0,
    Passive = 1,
}

/// <summary>Where this ability sits in a character kit or enemy catalog.</summary>
public enum CombatAbilityPlacement
{
    InnateActive = 0,
    TreeNode = 1,
    LeaderPassive = 2,
    CompanionPassive = 3,
    CorruptionPassive = 4,
    EnemyPassive = 5,
}

/// <summary>Which character statistic a data-driven passive changes.</summary>
public enum PassiveCharacterStatKind
{
    None = 0,
    CurrentHitPoints = 1,
    MaximumHitPoints = 2,
    DefenseChance = 3,
    DamageCaused = 4,
    Accuracy = 5,
    CriticalChance = 6,
    CriticalDamage = 7,
}

    /// <summary>Which skill statistic a data-driven passive changes.</summary>
public enum PassiveSkillStatKind
{
    None = 0,
    Damage = 1,
    Accuracy = 2,
    CorruptionCost = 3,
    CriticalChance = 4,
    /// <summary>Overrides <see cref="SkillTargetKind"/>; magnitude is the enum integer.</summary>
    TargetKind = 5,
    /// <summary>Added to <see cref="Models.SkillDefinition.HitCount"/> (Unload extra bullet).</summary>
    HitCount = 6,
    /// <summary>Added to <see cref="Models.SkillDefinition.ChanceToNotEndTurn"/>.</summary>
    ChanceToNotEndTurn = 7,
    /// <summary>Additive heal potency fraction (0.5 = Healing voice +50% more effective).</summary>
    HealEffectiveness = 8,
    /// <summary>Absolute chance that a HealHp roll is doubled after HealEffectiveness.</summary>
    HealDoubleChance = 9,
}

/// <summary>Resource used by ExtraStatsFromResource.</summary>
public enum PassiveResourceKind
{
    None = 0,
    MissingHitPointsFraction = 1,
    CurrentHitPointsFraction = 2,
    TokenStacks = 3,
    /// <summary>Floor of missing HP divided by <see cref="Models.PassiveEffectDefinition.Stacks"/> (chunk size).</summary>
    MissingHitPointsChunks = 4,
    /// <summary>Token stacks on the opposite combatant (the current attacker or skill target).</summary>
    OppositeTokenStacks = 5,
    /// <summary>Floor of own token stacks divided by <see cref="Models.PassiveEffectDefinition.Stacks"/> (chunk size).</summary>
    TokenStacksChunks = 6,
    /// <summary>Enemies this combatant has slain during the current battle.</summary>
    EnemiesDefeatedThisBattle = 7,
}

/// <summary>Who receives a CharacterStatChange.</summary>
public enum PassiveStatChangeTarget
{
    Self = 0,
    Ally = 1,
    SelfAndAlly = 2,
    /// <summary>The other combatant in the event (hit target, token recipient, attacker). Skips self.</summary>
    Opposite = 3,
    /// <summary>Every living combatant on the opposite side (Weaken/Vulnerability to all enemies).</summary>
    AllOpposites = 4,
    /// <summary>The event token recipient / other combatant, including self (extra stack of the applied token).</summary>
    EventRecipient = 5,
}

/// <summary>Apply versus remove for TokenManipulation.</summary>
public enum PassiveTokenManipulationMode
{
    Apply = 0,
    Remove = 1,
}

/// <summary>How UponApplying/ReceivingStatus matches tokens when RequiredStatus is omitted.</summary>
public enum PassiveStatusMatchKind
{
    ListedOrAny = 0,
    AnyStatus = 1,
    AnyBuff = 2,
    AnyDebuff = 3,
}

/// <summary>HP (or similar) comparison for UponStatThreshold.</summary>
public enum PassiveStatThresholdComparison
{
    BelowOrEqual = 0,
    AboveOrEqual = 1,
}

/// <summary>
/// Activation kinds for authored passives (data-driven engine in Phase C).
/// </summary>
public enum PassiveActivationKind
{
    Permanent = 0,
    WhileHavingStatus = 1,
    UponDamageTaken = 2,
    UponKill = 3,
    UponCriticalStrike = 4,
    UponApplyingStatus = 5,
    UponReceivingStatus = 6,
    UponStatThreshold = 7,
    OnTurnEnd = 8,
    UponHittingTargetWithStatus = 9,
    UponDealingDamage = 10,
    UponHealing = 11,
    OnTurnStart = 12,
    WhileOppositeHasStatus = 13,
    /// <summary>Fires once per player-chosen skill resolution (not follow-up invocations).</summary>
    UponUsingSkill = 14,
    /// <summary>Fires on living same-side allies when another ally takes damage (Maria team-hit passives).</summary>
    UponAllyDamageTaken = 15,
    /// <summary>Fires on living same-side allies when another ally deals damage.</summary>
    UponAllyDealingDamage = 16,
}

/// <summary>
/// Data-only effect operations for authored passives. Horse Boss summon is an enemy-only operation.
/// </summary>
public enum PassiveEffectOperationKind
{
    CharacterStatChange = 0,
    SkillStatChange = 1,
    TokenStatChange = 2,
    ExtraStatsFromResource = 3,
    TokenManipulation = 4,
    TurnManipulation = 5,
    Heal = 6,
    DealDamage = 7,
    TriggerDestabilization = 8,
    CastSkill = 9,
    SummonEnemy = 10,
}

/// <summary>Icon background only. The item icon itself is shared per icon family.</summary>
public enum CombatItemRarity
{
    Common = 0,
    Rare = 1,
    Epic = 2,
    Legendary = 3,
}

public enum CombatItemKind
{
    IndividualStatus = 0,
    Thematic = 1,
    Utility = 2,
}

public enum CombatItemStatKind
{
    Health = 0,
    Defense = 1,
    CriticalChance = 2,
}

/// <summary>Absolute = fixed GDD table. Relative = percent of current MaxHp, or percentage points for Defense/Crit.</summary>
public enum CombatItemModifierScale
{
    Absolute = 0,
    Relative = 1,
}

public enum CombatItemUtilityKind
{
    None = 0,
    ResetAllSkillTrees = 1,
    ResetBuckSkillTree = 2,
    ResetWulfricSkillTree = 3,
    /// <summary>Spec "Reset Matsuda" — combat characterId is maria. Do not invent a Matsuda kit.</summary>
    ResetMariaSkillTree = 4,
}
