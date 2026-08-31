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
    Block = 0,
    BlockPlus = 1,
    Dodge = 2,
    Blind = 3,
    Taunt = 4,
    Stealth = 5,
    Combo = 6,
    Stun = 7,
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
