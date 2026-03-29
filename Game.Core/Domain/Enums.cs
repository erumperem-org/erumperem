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
    HealCorruption = 5,
    ApplyStun = 6,
    HealHpPercent = 7,
    IncreaseCorruption = 8,
}

public enum SkillTargetKind
{
    Enemy = 0,
    Ally = 1,
    Self = 2,
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
}
