import fs from "fs";
import path from "path";
import crypto from "crypto";

const ROOT = "e:/projects/erumperem";
const MARIA_DIR = path.join(ROOT, "Assets/_Project/ScriptableObjects/Combat/Maria");
const CHAR_DIR = path.join(ROOT, "Assets/_Project/ScriptableObjects/Characters");
const DATA_DIR = path.join(ROOT, "Assets/StreamingAssets/Data");
const NODES_DIR = path.join(ROOT, "Assets/_Project/Resources/SkillTreeNodes");
const VISUAL_ASSET = path.join(ROOT, "Assets/_Project/Scripts/Combat/SkillVisualCatalog.asset");

const SCRIPT_GUID = "c0ab11117e8f4d2e9a3b5c6d7e8f9012";
const KIT_SCRIPT_GUID = "f3de4444a1bc4b5b2d6e8f9012345678";
const ALLY_SCRIPT_GUID = "cc11dd22ee33ff44556677889900aabb";

const T = { Strength: 10, Defense: 11, Weaken: 12, Vulnerability: 13, Confusion: 14, LuckyShot: 16, Dexterity: 17, Regeneration: 21, Clumsy: 22 };
const KIND = { Active: 0, Passive: 1 };
const PLACE = { Innate: 0, Tree: 1, Leader: 2, Companion: 3, Corruption: 4 };
const TARGET = { OneEnemy: 0, AllEnemies: 4, SelfOrAlly: 5, SelfAndAlly: 6 };
const OP = { Char: 0, Skill: 1, TokenStat: 2, TokenManip: 4, Heal: 6, Damage: 7, Cast: 9 };
const ACT = { Perm: 0, WhileStatus: 1, DmgTaken: 2, Apply: 5, HitStatus: 9, DealDmg: 10, Heal: 11, TurnStart: 12, WhileOpp: 13, UseSkill: 14, AllyDmgTaken: 15, AllyDealDmg: 16 };
const MATCH = { AnyStatus: 1, AnyBuff: 2, AnyDebuff: 3 };
const SCOPE = { Self: 0, SelfAndAlly: 2, Opposite: 3, AllOpposites: 4, EventRecipient: 5 };
const SKILL = { Acc: 2, HealEff: 8, HealDbl: 9 };
const STAT = { Defense: 3, CritDmg: 7 };

const OLD_SKILL_IDS = new Set([
  "mariaBasicHit", "mariaHealVoice", "mariaScreamAttack", "mariaDamageBuff",
  "mariaEchoHeal", "mariaCleanse", "mariaResurrection", "mariaChanceBuff",
  "mariaDefenseBuff", "mariaShow", "mariaScreechNoise", "mariaPiercingYell", "mariaChaosMelody",
]);

const NODE_ID_REMAP = {
  m_lf_t1_p1: "maria_tree1_tier1_passive1", m_lf_t1_p2: "maria_tree1_tier1_passive2", m_lf_t1_p3: "maria_tree1_tier1_passive3", mariaEchoHeal: "maria_tree1_tier1_active",
  m_lf_t2_p1: "maria_tree1_tier2_passive1", m_lf_t2_p2: "maria_tree1_tier2_passive2", m_lf_t2_p3: "maria_tree1_tier2_passive3", mariaCleanse: "maria_tree1_tier2_active",
  m_lf_t3_p1: "maria_tree1_tier3_passive1", m_lf_t3_p2: "maria_tree1_tier3_passive2", m_lf_t3_p3: "maria_tree1_tier3_passive3", mariaResurrection: "maria_tree1_tier3_active",
  m_bh_t1_p1: "maria_tree2_tier1_passive1", m_bh_t1_p2: "maria_tree2_tier1_passive2", m_bh_t1_p3: "maria_tree2_tier1_passive3", mariaChanceBuff: "maria_tree2_tier1_active",
  m_bh_t2_p1: "maria_tree2_tier2_passive1", m_bh_t2_p2: "maria_tree2_tier2_passive2", m_bh_t2_p3: "maria_tree2_tier2_passive3", mariaDefenseBuff: "maria_tree2_tier2_active",
  m_bh_t3_p1: "maria_tree2_tier3_passive1", m_bh_t3_p2: "maria_tree2_tier3_passive2", m_bh_t3_p3: "maria_tree2_tier3_passive3", mariaShow: "maria_tree2_tier3_active",
  m_ds_t1_p1: "maria_tree3_tier1_passive1", m_ds_t1_p2: "maria_tree3_tier1_passive2", m_ds_t1_p3: "maria_tree3_tier1_passive3", mariaScreechNoise: "maria_tree3_tier1_active",
  m_ds_t2_p1: "maria_tree3_tier2_passive1", m_ds_t2_p2: "maria_tree3_tier2_passive2", m_ds_t2_p3: "maria_tree3_tier2_passive3", mariaPiercingYell: "maria_tree3_tier2_active",
  m_ds_t3_p1: "maria_tree3_tier3_passive1", m_ds_t3_p2: "maria_tree3_tier3_passive2", m_ds_t3_p3: "maria_tree3_tier3_passive3", mariaChaosMelody: "maria_tree3_tier3_active",
};

const VISUAL_REMAP = {
  mariaBasicHit: ["maria_innate_active1", "Sound strike"],
  mariaHealVoice: ["maria_innate_active2", "Healing voice"],
  mariaScreamAttack: ["maria_innate_active3", "Amplified Scream"],
  mariaDamageBuff: ["maria_innate_active4", "Inspirational song"],
  mariaEchoHeal: ["maria_tree1_tier1_active", "Echoing regeneration"],
  mariaCleanse: ["maria_tree1_tier2_active", "Refreshing song"],
  mariaResurrection: ["maria_tree1_tier3_active", "Resurrection Hymn"],
  mariaChanceBuff: ["maria_tree2_tier1_active", "Higher Pitch"],
  mariaDefenseBuff: ["maria_tree2_tier2_active", "Protection song"],
  mariaShow: ["maria_tree2_tier3_active", "Dance of the Revolution"],
  mariaScreechNoise: ["maria_tree3_tier1_active", "Screech Noise"],
  mariaPiercingYell: ["maria_tree3_tier2_active", "Piercing Yell"],
  mariaChaosMelody: ["maria_tree3_tier3_active", "Chaos Melody"],
};

function newGuid() {
  return crypto.randomUUID().replaceAll("-", "");
}

function yamlHeader(name, scriptGuid = SCRIPT_GUID) {
  return `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${scriptGuid}, type: 3}
  m_Name: ${name}
  m_EditorClassIdentifier: 
`;
}

function metaText(guid) {
  return `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;
}

function dumpSeq(items) {
  if (!items || items.length === 0) return "[]";
  const lines = [""];
  for (const item of items) {
    let first = true;
    for (const [key, value] of Object.entries(item)) {
      const prefix = first ? "- " : "  ";
      first = false;
      lines.push(`  ${prefix}${key}: ${value}`);
    }
  }
  return lines.join("\n");
}

function cond(activation, statusMatch = 0, skillId = "", threshold = 0) {
  return {
    Activation: activation,
    HasRequiredStatus: 0,
    RequiredStatus: 0,
    StatusMatch: statusMatch,
    HitPointsLostPerTrigger: 0,
    StatThresholdFraction: threshold,
    StatThresholdComparison: 0,
    SkillId: skillId,
  };
}

function pfx(operation, opts = {}) {
  return {
    Operation: operation,
    Magnitude: opts.magnitude ?? 0,
    HasToken: opts.hasToken ?? 0,
    Token: opts.token ?? 0,
    SkillId: opts.skillId ?? "",
    SummonEnemyId: "",
    Stacks: opts.stacks ?? 0,
    ChanceToTrigger: opts.chance ?? 1,
    MaxTriggersPerBattle: opts.maxBattle ?? 0,
    MaxTriggersPerTurn: 0,
    CharacterStat: opts.characterStat ?? 0,
    SkillStat: opts.skillStat ?? 0,
    Resource: 0,
    StatChangeTarget: opts.target ?? 0,
    TokenManipulationMode: 0,
    HasResourceToken: 0,
    ResourceToken: 0,
    ScaleStacksPerSourceStack: opts.scaleSrc ?? 0,
    ScaleStacksSourceDivisor: opts.scaleDiv ?? 0,
    ExpiresAtEndOfOpposingSideTurn: 0,
    SkipEndOfTurnDecayChance: 0,
  };
}

function fxToken(token, stacks) {
  return {
    Type: 0, Chance: 1, Stacks: stacks, Potency: 0, AmountMax: 0, Duration: 0, Steps: 0, EffectScope: 0,
    HasToken: 1, Token: token, HasDot: 0, Dot: 0, HasScaleFromToken: 0, ScaleFromToken: 0,
    ScaleStacksPerSourceStack: 0, ScaleStacksSourceDivisor: 1,
  };
}

function fxHeal(potency, amountMax) {
  return {
    Type: 4, Chance: 1, Stacks: 0, Potency: potency, AmountMax: amountMax, Duration: 0, Steps: 0, EffectScope: 0,
    HasToken: 0, Token: 0, HasDot: 0, Dot: 0, HasScaleFromToken: 0, ScaleFromToken: 0,
    ScaleStacksPerSourceStack: 0, ScaleStacksSourceDivisor: 1,
  };
}

function fxCleanse() {
  return { ...fxHeal(0, 0), Type: 8 };
}

function writeAbility(spec, guid) {
  const prereqs = spec.prereqs ?? [];
  const prereqYaml = prereqs.length ? `\n${prereqs.map((id) => `  - ${id}`).join("\n")}` : " []";
  const body = yamlHeader(spec.id) + `  _abilityId: ${spec.id}
  _displayName: ${spec.display}
  _ownerCharacterId: maria
  _abilityKind: ${spec.kind}
  _placement: ${spec.placement}
  _treeIndex: ${spec.tree ?? 1}
  _tierIndex: ${spec.tier ?? 1}
  _passiveIndex: ${spec.passiveIndex ?? 1}
  _corruptionMinTier: ${spec.corruptionMin ?? 0}
  _designerNotes: Maria kit. Duplicate this asset, change abilityId, Export Catalog.
  _activeSkillTypeLabel: Active
  _activeSkillDamageElement: ${spec.kind === KIND.Active ? 3 : 0}
  _baseDamageMinimum: ${spec.dmgMin ?? 0}
  _baseDamageMaximum: ${spec.dmgMax ?? 0}
  _baseCriticalHitChanceFraction: ${spec.crit ?? 0}
  _baseHitAccuracyFraction: ${spec.acc ?? 1}
  _targetSelectionKind: ${spec.target ?? 0}
  _aiAbsoluteChanceToConsiderWhenEligible: 1
  _aiOnlyEligibleWhenOwnHpFractionBelow: 1
  _corruptionCostAddedWhenPlayerCasts: ${spec.cost ?? 0}
  _hitCount: 1
  _chanceToNotEndTurn: 0
  _followUpSkillIds: []
  _grantsBonusActionsToAllies: 0
  _accuracyPenaltyPerLivingEnemy: 0
  _hasBonusDamagePerOwnToken: 0
  _bonusDamagePerOwnToken: 0
  _bonusDamagePerOwnTokenStacks: 1
  _computeFromDebuffTypesOnTarget: 0
  _damagePerDistinctDebuffType: 0
  _critChancePerDistinctDebuffType: 0
  _accuracyPerDistinctDebuffType: 0
  _canTargetDeadAllies: ${spec.canTargetDead ?? 0}
  _effectsAppliedAfterSuccessfulHit: ${dumpSeq(spec.effects)}
  _requiredPartyRole: ${spec.role ?? 0}
  _chanceToTrigger: 1
  _maxTriggersPerBattle: ${spec.maxBattle ?? 0}
  _maxTriggersPerTurn: 0
  _passiveConditions: ${dumpSeq(spec.conditions)}
  _passiveEffects: ${dumpSeq(spec.passiveEffects)}
  _treeNodeUnlockCost: 1
  _treeNodePrerequisiteAbilityIds:${prereqYaml}
`;
  fs.mkdirSync(MARIA_DIR, { recursive: true });
  fs.writeFileSync(path.join(MARIA_DIR, `${spec.id}.asset`), body);
  fs.writeFileSync(path.join(MARIA_DIR, `${spec.id}.asset.meta`), metaText(guid));
}

function treePrereqs(tree, tier) {
  return [1, 2, 3].map((index) => `maria_tree${tree}_tier${tier}_passive${index}`);
}

function buildSpecs() {
  const specs = [];
  const add = (spec) => specs.push(spec);

  add({ id: "maria_innate_active1", display: "Sound strike", kind: KIND.Active, placement: PLACE.Innate, dmgMin: 4, dmgMax: 8, crit: 0.01, acc: 0.9, target: TARGET.OneEnemy });
  add({ id: "maria_innate_active2", display: "Healing voice", kind: KIND.Active, placement: PLACE.Innate, acc: 0.8, target: TARGET.SelfOrAlly, cost: 4, effects: [fxHeal(5, 10)] });
  add({ id: "maria_innate_active3", display: "Amplified Scream", kind: KIND.Active, placement: PLACE.Innate, dmgMin: 3, dmgMax: 6, acc: 0.7, target: TARGET.AllEnemies, cost: 3, effects: [fxToken(T.Vulnerability, 2)] });
  add({ id: "maria_innate_active4", display: "Inspirational song", kind: KIND.Active, placement: PLACE.Innate, acc: 0.75, target: TARGET.SelfAndAlly, cost: 4, effects: [fxToken(T.Strength, 3)] });
  add({ id: "maria_leader_passive1", display: "Leader: +1 extra debuff stack", kind: KIND.Passive, placement: PLACE.Leader, role: 1, conditions: [cond(ACT.Apply, MATCH.AnyDebuff)], passiveEffects: [pfx(OP.TokenManip, { stacks: 1, target: SCOPE.EventRecipient })] });
  add({ id: "maria_companion_passive1", display: "Companion: +1 extra buff stack", kind: KIND.Passive, placement: PLACE.Companion, role: 2, conditions: [cond(ACT.Apply, MATCH.AnyBuff)], passiveEffects: [pfx(OP.TokenManip, { stacks: 1, target: SCOPE.EventRecipient })] });

  for (const [tier, efficiency, proc] of [[1, 0.25, 0.05], [2, 0.5, 0.1], [3, 1.0, 0.15]]) {
    add({
      id: `maria_corruption_tier${tier}_passive1`,
      display: `Corruption T${tier}: token efficiency and basic proc`,
      kind: KIND.Passive, placement: PLACE.Corruption, corruptionMin: tier,
      conditions: [cond(ACT.Perm), cond(ACT.Apply)],
      passiveEffects: [
        pfx(OP.TokenStat, { magnitude: efficiency, target: SCOPE.Self }),
        pfx(OP.TokenStat, { magnitude: efficiency, target: SCOPE.Opposite }),
        pfx(OP.Cast, { skillId: "maria_innate_active1", chance: proc, target: SCOPE.Opposite }),
      ],
    });
  }

  add({ id: "maria_tree1_tier1_passive1", display: "Healing voice +50% effective", kind: KIND.Passive, placement: PLACE.Tree, tree: 1, tier: 1, passiveIndex: 1, conditions: [cond(ACT.Perm)], passiveEffects: [pfx(OP.Skill, { magnitude: 0.5, skillId: "maria_innate_active2", skillStat: SKILL.HealEff })] });
  add({ id: "maria_tree1_tier1_passive2", display: "2 Strength after healing", kind: KIND.Passive, placement: PLACE.Tree, tree: 1, tier: 1, passiveIndex: 2, conditions: [cond(ACT.Heal)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Strength, stacks: 2, target: SCOPE.Self })] });
  add({ id: "maria_tree1_tier1_passive3", display: "25% chance 2 Defense on heal target", kind: KIND.Passive, placement: PLACE.Tree, tree: 1, tier: 1, passiveIndex: 3, conditions: [cond(ACT.Heal)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Defense, stacks: 2, chance: 0.25, target: SCOPE.Opposite })] });
  add({ id: "maria_tree1_tier1_active", display: "Echoing regeneration", kind: KIND.Active, placement: PLACE.Tree, tree: 1, tier: 1, acc: 0.75, target: TARGET.SelfAndAlly, cost: 6, effects: [fxHeal(3, 6), fxToken(T.Regeneration, 3)], prereqs: treePrereqs(1, 1) });
  add({ id: "maria_tree1_tier2_passive1", display: "Applying a buff heals the target 3", kind: KIND.Passive, placement: PLACE.Tree, tree: 1, tier: 2, passiveIndex: 1, conditions: [cond(ACT.Apply, MATCH.AnyBuff)], passiveEffects: [pfx(OP.Heal, { magnitude: 3, target: SCOPE.Opposite })] });
  add({ id: "maria_tree1_tier2_passive2", display: "Applying a debuff heals self 3", kind: KIND.Passive, placement: PLACE.Tree, tree: 1, tier: 2, passiveIndex: 2, conditions: [cond(ACT.Apply, MATCH.AnyDebuff)], passiveEffects: [pfx(OP.Heal, { magnitude: 3, target: SCOPE.Self })] });
  add({ id: "maria_tree1_tier2_passive3", display: "Battle start 3 Regeneration on team", kind: KIND.Passive, placement: PLACE.Tree, tree: 1, tier: 2, passiveIndex: 3, maxBattle: 1, conditions: [cond(ACT.TurnStart)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Regeneration, stacks: 3, maxBattle: 1, target: SCOPE.SelfAndAlly })] });
  add({ id: "maria_tree1_tier2_active", display: "Refreshing song", kind: KIND.Active, placement: PLACE.Tree, tree: 1, tier: 2, acc: 0.6, target: TARGET.SelfAndAlly, cost: 6, effects: [fxHeal(5, 10), fxCleanse()], prereqs: treePrereqs(1, 2) });
  add({ id: "maria_tree1_tier3_passive1", display: "20% chance heal half damage dealt", kind: KIND.Passive, placement: PLACE.Tree, tree: 1, tier: 3, passiveIndex: 1, conditions: [cond(ACT.DealDmg)], passiveEffects: [pfx(OP.Heal, { magnitude: 0.5, chance: 0.2, target: SCOPE.Self })] });
  add({ id: "maria_tree1_tier3_passive2", display: "Below 25% HP at turn start: 5 Regeneration once", kind: KIND.Passive, placement: PLACE.Tree, tree: 1, tier: 3, passiveIndex: 2, maxBattle: 1, conditions: [cond(ACT.TurnStart, 0, "", 0.25)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Regeneration, stacks: 5, maxBattle: 1, target: SCOPE.Self })] });
  add({ id: "maria_tree1_tier3_passive3", display: "Healing skills 10% chance to double", kind: KIND.Passive, placement: PLACE.Tree, tree: 1, tier: 3, passiveIndex: 3, conditions: [cond(ACT.Perm)], passiveEffects: [pfx(OP.Skill, { magnitude: 0.1, skillStat: SKILL.HealDbl })] });
  add({ id: "maria_tree1_tier3_active", display: "Resurrection Hymn", kind: KIND.Active, placement: PLACE.Tree, tree: 1, tier: 3, acc: 1, target: TARGET.SelfAndAlly, cost: 20, canTargetDead: 1, effects: [fxHeal(40, 50), fxCleanse(), fxToken(T.Strength, 2), fxToken(T.Defense, 2)], prereqs: treePrereqs(1, 3) });

  add({ id: "maria_tree2_tier1_passive1", display: "Inspirational song +10% accuracy", kind: KIND.Passive, placement: PLACE.Tree, tree: 2, tier: 1, passiveIndex: 1, conditions: [cond(ACT.Perm)], passiveEffects: [pfx(OP.Skill, { magnitude: 0.1, skillId: "maria_innate_active4", skillStat: SKILL.Acc })] });
  add({ id: "maria_tree2_tier1_passive2", display: "25% team heal 3 when a buffed ally hits", kind: KIND.Passive, placement: PLACE.Tree, tree: 2, tier: 1, passiveIndex: 2, conditions: [cond(ACT.DealDmg, MATCH.AnyBuff), cond(ACT.AllyDealDmg, MATCH.AnyBuff)], passiveEffects: [pfx(OP.Heal, { magnitude: 3, chance: 0.25, target: SCOPE.SelfAndAlly })] });
  add({ id: "maria_tree2_tier1_passive3", display: "Token on ally also grants +1 Strength", kind: KIND.Passive, placement: PLACE.Tree, tree: 2, tier: 1, passiveIndex: 3, conditions: [cond(ACT.Apply, MATCH.AnyStatus)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Strength, stacks: 1, target: SCOPE.Opposite })] });
  add({ id: "maria_tree2_tier1_active", display: "Higher Pitch", kind: KIND.Active, placement: PLACE.Tree, tree: 2, tier: 1, acc: 0.85, target: TARGET.SelfOrAlly, cost: 4, effects: [fxToken(T.Dexterity, 3), fxToken(T.LuckyShot, 2)], prereqs: treePrereqs(2, 1) });
  add({ id: "maria_tree2_tier2_passive1", display: "Healing an ally grants +1 Strength", kind: KIND.Passive, placement: PLACE.Tree, tree: 2, tier: 2, passiveIndex: 1, conditions: [cond(ACT.Heal)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Strength, stacks: 1, target: SCOPE.Opposite })] });
  add({ id: "maria_tree2_tier2_passive2", display: "70% chance +1 Defense when applying a token to an ally", kind: KIND.Passive, placement: PLACE.Tree, tree: 2, tier: 2, passiveIndex: 2, conditions: [cond(ACT.Apply, MATCH.AnyStatus)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Defense, stacks: 1, chance: 0.7, target: SCOPE.Opposite })] });
  add({ id: "maria_tree2_tier2_passive3", display: "Scream +20% accuracy", kind: KIND.Passive, placement: PLACE.Tree, tree: 2, tier: 2, passiveIndex: 3, conditions: [cond(ACT.Perm)], passiveEffects: [pfx(OP.Skill, { magnitude: 0.2, skillId: "maria_innate_active3", skillStat: SKILL.Acc })] });
  add({ id: "maria_tree2_tier2_active", display: "Protection song", kind: KIND.Active, placement: PLACE.Tree, tree: 2, tier: 2, acc: 0.75, target: TARGET.SelfAndAlly, cost: 5, effects: [fxToken(T.Defense, 4)], prereqs: treePrereqs(2, 2) });
  add({ id: "maria_tree2_tier3_passive1", display: "+1 Dexterity to team on enemy hit", kind: KIND.Passive, placement: PLACE.Tree, tree: 2, tier: 3, passiveIndex: 1, conditions: [cond(ACT.DealDmg)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Dexterity, stacks: 1, target: SCOPE.SelfAndAlly })] });
  add({ id: "maria_tree2_tier3_passive2", display: "Team takes 10% less damage while buffed", kind: KIND.Passive, placement: PLACE.Tree, tree: 2, tier: 3, passiveIndex: 2, conditions: [cond(ACT.WhileStatus, MATCH.AnyBuff)], passiveEffects: [pfx(OP.Char, { magnitude: 0.1, characterStat: STAT.Defense, target: SCOPE.SelfAndAlly })] });
  add({ id: "maria_tree2_tier3_passive3", display: "Buffed team crits deal triple", kind: KIND.Passive, placement: PLACE.Tree, tree: 2, tier: 3, passiveIndex: 3, conditions: [cond(ACT.WhileStatus, MATCH.AnyBuff)], passiveEffects: [pfx(OP.Char, { magnitude: 0.5, characterStat: STAT.CritDmg, target: SCOPE.SelfAndAlly })] });
  add({ id: "maria_tree2_tier3_active", display: "Dance of the Revolution", kind: KIND.Active, placement: PLACE.Tree, tree: 2, tier: 3, acc: 0.85, target: TARGET.SelfAndAlly, cost: 9, effects: [fxToken(T.Strength, 3), fxToken(T.Defense, 3), fxToken(T.Dexterity, 3)], prereqs: treePrereqs(2, 3) });

  add({ id: "maria_tree3_tier1_passive1", display: "Applying a debuff deals damage equal to stacks applied", kind: KIND.Passive, placement: PLACE.Tree, tree: 3, tier: 1, passiveIndex: 1, conditions: [cond(ACT.Apply, MATCH.AnyDebuff)], passiveEffects: [pfx(OP.Damage, { magnitude: 1, target: SCOPE.Opposite, scaleSrc: 1, scaleDiv: 1 })] });
  add({ id: "maria_tree3_tier1_passive2", display: "50% chance heal 6 when hitting a debuffed enemy", kind: KIND.Passive, placement: PLACE.Tree, tree: 3, tier: 1, passiveIndex: 2, conditions: [cond(ACT.HitStatus, MATCH.AnyDebuff)], passiveEffects: [pfx(OP.Heal, { magnitude: 6, chance: 0.5, target: SCOPE.Self })] });
  add({ id: "maria_tree3_tier1_passive3", display: "10% less damage from debuffed enemies", kind: KIND.Passive, placement: PLACE.Tree, tree: 3, tier: 1, passiveIndex: 3, conditions: [cond(ACT.WhileOpp, MATCH.AnyDebuff)], passiveEffects: [pfx(OP.Char, { magnitude: 0.1, characterStat: STAT.Defense })] });
  add({ id: "maria_tree3_tier1_active", display: "Screech Noise", kind: KIND.Active, placement: PLACE.Tree, tree: 3, tier: 1, dmgMin: 10, dmgMax: 12, acc: 0.8, target: TARGET.OneEnemy, cost: 4, effects: [fxToken(T.Clumsy, 2)], prereqs: treePrereqs(3, 1) });
  add({ id: "maria_tree3_tier2_passive1", display: "Basic hit 40% chance 2 Weaken", kind: KIND.Passive, placement: PLACE.Tree, tree: 3, tier: 2, passiveIndex: 1, conditions: [cond(ACT.HitStatus, 0, "maria_innate_active1")], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Weaken, stacks: 2, chance: 0.4, target: SCOPE.Opposite })] });
  add({ id: "maria_tree3_tier2_passive2", display: "Healing skill 40% chance 1 Weaken all enemies", kind: KIND.Passive, placement: PLACE.Tree, tree: 3, tier: 2, passiveIndex: 2, conditions: ["maria_innate_active2", "maria_tree1_tier1_active", "maria_tree1_tier2_active", "maria_tree1_tier3_active"].map((skillId) => cond(ACT.UseSkill, 0, skillId)), passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Weaken, stacks: 1, chance: 0.4, target: SCOPE.AllOpposites })] });
  add({ id: "maria_tree3_tier2_passive3", display: "Buff skill 40% chance 1 Vulnerability all enemies", kind: KIND.Passive, placement: PLACE.Tree, tree: 3, tier: 2, passiveIndex: 3, conditions: ["maria_innate_active4", "maria_tree2_tier1_active", "maria_tree2_tier2_active", "maria_tree2_tier3_active"].map((skillId) => cond(ACT.UseSkill, 0, skillId)), passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Vulnerability, stacks: 1, chance: 0.4, target: SCOPE.AllOpposites })] });
  add({ id: "maria_tree3_tier2_active", display: "Piercing Yell", kind: KIND.Active, placement: PLACE.Tree, tree: 3, tier: 2, dmgMin: 7, dmgMax: 9, acc: 0.7, target: TARGET.AllEnemies, cost: 6, effects: [fxToken(T.Clumsy, 3)], prereqs: treePrereqs(3, 2) });
  add({ id: "maria_tree3_tier3_passive1", display: "When team is hit, apply 1 Weaken to the attacker", kind: KIND.Passive, placement: PLACE.Tree, tree: 3, tier: 3, passiveIndex: 1, conditions: [cond(ACT.DmgTaken), cond(ACT.AllyDmgTaken)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Weaken, stacks: 1, target: SCOPE.Opposite })] });
  add({ id: "maria_tree3_tier3_passive2", display: "Debuff tokens +50% effective", kind: KIND.Passive, placement: PLACE.Tree, tree: 3, tier: 3, passiveIndex: 2, conditions: [cond(ACT.Perm)], passiveEffects: [T.Weaken, T.Vulnerability, T.Clumsy, T.Confusion, 18, 19, 20, 15].map((token) => pfx(OP.TokenStat, { magnitude: 0.5, hasToken: 1, token, target: SCOPE.Opposite })) });
  add({ id: "maria_tree3_tier3_passive3", display: "When team is hit, apply 1 Vulnerability to the attacker", kind: KIND.Passive, placement: PLACE.Tree, tree: 3, tier: 3, passiveIndex: 3, conditions: [cond(ACT.DmgTaken), cond(ACT.AllyDmgTaken)], passiveEffects: [pfx(OP.TokenManip, { hasToken: 1, token: T.Vulnerability, stacks: 1, target: SCOPE.Opposite })] });
  add({ id: "maria_tree3_tier3_active", display: "Chaos Melody", kind: KIND.Active, placement: PLACE.Tree, tree: 3, tier: 3, dmgMin: 2, dmgMax: 6, acc: 0.7, target: TARGET.AllEnemies, cost: 8, effects: [fxToken(T.Confusion, 2)], prereqs: treePrereqs(3, 3) });

  return specs;
}

function condJson(activation, statusMatch, skillId, threshold) {
  const item = { activation };
  if (statusMatch) item.statusMatch = statusMatch;
  if (skillId) item.skillId = skillId;
  if (threshold) item.statThresholdFraction = threshold;
  return item;
}

function effectJson(operation, opts = {}) {
  const item = { chanceToTrigger: opts.chance ?? 1, operation };
  if (opts.magnitude !== undefined) item.magnitude = opts.magnitude;
  if (opts.token) item.token = opts.token;
  if (opts.skillId) item.skillId = opts.skillId;
  if (opts.stacks !== undefined) item.stacks = opts.stacks;
  if (opts.maxBattle) item.maxTriggersPerBattle = opts.maxBattle;
  if (opts.characterStat) item.characterStat = opts.characterStat;
  if (opts.skillStat) item.skillStat = opts.skillStat;
  if (opts.target) item.statChangeTarget = opts.target;
  if (opts.scaleSrc) item.scaleStacksPerSourceStack = opts.scaleSrc;
  if (opts.scaleDiv) item.scaleStacksSourceDivisor = opts.scaleDiv;
  return item;
}

function skillDef(id, name, opts) {
  const item = {
    id, name, corruptionCost: opts.cost, element: "Anomaly", type: "Active", targetKind: opts.target,
    baseDamage: { min: opts.dmg?.[0] ?? 0, max: opts.dmg?.[1] ?? 0 },
    baseCritChance: opts.crit ?? 0, accuracy: opts.acc,
  };
  if (opts.canTargetDead) item.canTargetDeadAllies = true;
  item.effectsOnHit = opts.effects ?? [];
  return item;
}

function tokenFx(token, stacks) { return { type: "ApplyToken", token, stacks, chance: 1 }; }
function healFx(potency, amountMax) { return { chance: 1, type: "HealHp", potency, amountMax }; }
function cleanseFx() { return { chance: 1, type: "RemoveAllDebuffTokens" }; }

function mariaSkills() {
  return [
    skillDef("maria_innate_active1", "Sound strike", { cost: 0, target: "OneEnemy", dmg: [4, 8], crit: 0.01, acc: 0.9 }),
    skillDef("maria_innate_active2", "Healing voice", { cost: 4, target: "SelfOrAlly", acc: 0.8, effects: [healFx(5, 10)] }),
    skillDef("maria_innate_active3", "Amplified Scream", { cost: 3, target: "AllEnemies", dmg: [3, 6], acc: 0.7, effects: [tokenFx("Vulnerability", 2)] }),
    skillDef("maria_innate_active4", "Inspirational song", { cost: 4, target: "SelfAndAlly", acc: 0.75, effects: [tokenFx("Strength", 3)] }),
    skillDef("maria_tree1_tier1_active", "Echoing regeneration", { cost: 6, target: "SelfAndAlly", acc: 0.75, effects: [healFx(3, 6), tokenFx("Regeneration", 3)] }),
    skillDef("maria_tree1_tier2_active", "Refreshing song", { cost: 6, target: "SelfAndAlly", acc: 0.6, effects: [healFx(5, 10), cleanseFx()] }),
    skillDef("maria_tree1_tier3_active", "Resurrection Hymn", { cost: 20, target: "SelfAndAlly", acc: 1, canTargetDead: true, effects: [healFx(40, 50), cleanseFx(), tokenFx("Strength", 2), tokenFx("Defense", 2)] }),
    skillDef("maria_tree2_tier1_active", "Higher Pitch", { cost: 4, target: "SelfOrAlly", acc: 0.85, effects: [tokenFx("Dexterity", 3), tokenFx("LuckyShot", 2)] }),
    skillDef("maria_tree2_tier2_active", "Protection song", { cost: 5, target: "SelfAndAlly", acc: 0.75, effects: [tokenFx("Defense", 4)] }),
    skillDef("maria_tree2_tier3_active", "Dance of the Revolution", { cost: 9, target: "SelfAndAlly", acc: 0.85, effects: [tokenFx("Strength", 3), tokenFx("Defense", 3), tokenFx("Dexterity", 3)] }),
    skillDef("maria_tree3_tier1_active", "Screech Noise", { cost: 4, target: "OneEnemy", dmg: [10, 12], acc: 0.8, effects: [tokenFx("Clumsy", 2)] }),
    skillDef("maria_tree3_tier2_active", "Piercing Yell", { cost: 6, target: "AllEnemies", dmg: [7, 9], acc: 0.7, effects: [tokenFx("Clumsy", 3)] }),
    skillDef("maria_tree3_tier3_active", "Chaos Melody", { cost: 8, target: "AllEnemies", dmg: [2, 6], acc: 0.7, effects: [tokenFx("Confusion", 2)] }),
  ];
}

function pdef(id, opts) {
  const item = { id, requiredPartyRole: opts.role ?? "Any", corruptionMinTier: opts.corruptionMin ?? 0, chanceToTrigger: 1 };
  if (opts.maxBattle) item.maxTriggersPerBattle = opts.maxBattle;
  item.conditions = opts.conditions;
  item.effects = opts.effects;
  return item;
}

function mariaPassives() {
  const passives = [
    pdef("maria_leader_passive1", { role: "Leader", conditions: [condJson("UponApplyingStatus", "AnyDebuff")], effects: [effectJson("TokenManipulation", { stacks: 1, target: "EventRecipient" })] }),
    pdef("maria_companion_passive1", { role: "Companion", conditions: [condJson("UponApplyingStatus", "AnyBuff")], effects: [effectJson("TokenManipulation", { stacks: 1, target: "EventRecipient" })] }),
  ];
  for (const [tier, efficiency, proc] of [[1, 0.25, 0.05], [2, 0.5, 0.1], [3, 1.0, 0.15]]) {
    passives.push(pdef(`maria_corruption_tier${tier}_passive1`, {
      corruptionMin: tier,
      conditions: [condJson("Permanent"), condJson("UponApplyingStatus")],
      effects: [
        effectJson("TokenStatChange", { magnitude: efficiency, target: "Self" }),
        effectJson("TokenStatChange", { magnitude: efficiency, target: "Opposite" }),
        effectJson("CastSkill", { skillId: "maria_innate_active1", chance: proc, target: "Opposite" }),
      ],
    }));
  }
  passives.push(
    pdef("maria_tree1_tier1_passive1", { conditions: [condJson("Permanent")], effects: [effectJson("SkillStatChange", { magnitude: 0.5, skillId: "maria_innate_active2", skillStat: "HealEffectiveness" })] }),
    pdef("maria_tree1_tier1_passive2", { conditions: [condJson("UponHealing")], effects: [effectJson("TokenManipulation", { token: "Strength", stacks: 2, target: "Self" })] }),
    pdef("maria_tree1_tier1_passive3", { conditions: [condJson("UponHealing")], effects: [effectJson("TokenManipulation", { token: "Defense", stacks: 2, chance: 0.25, target: "Opposite" })] }),
    pdef("maria_tree1_tier2_passive1", { conditions: [condJson("UponApplyingStatus", "AnyBuff")], effects: [effectJson("Heal", { magnitude: 3, target: "Opposite" })] }),
    pdef("maria_tree1_tier2_passive2", { conditions: [condJson("UponApplyingStatus", "AnyDebuff")], effects: [effectJson("Heal", { magnitude: 3, target: "Self" })] }),
    pdef("maria_tree1_tier2_passive3", { maxBattle: 1, conditions: [condJson("OnTurnStart")], effects: [effectJson("TokenManipulation", { token: "Regeneration", stacks: 3, maxBattle: 1, target: "SelfAndAlly" })] }),
    pdef("maria_tree1_tier3_passive1", { conditions: [condJson("UponDealingDamage")], effects: [effectJson("Heal", { magnitude: 0.5, chance: 0.2, target: "Self" })] }),
    pdef("maria_tree1_tier3_passive2", { maxBattle: 1, conditions: [condJson("OnTurnStart", undefined, undefined, 0.25)], effects: [effectJson("TokenManipulation", { token: "Regeneration", stacks: 5, maxBattle: 1, target: "Self" })] }),
    pdef("maria_tree1_tier3_passive3", { conditions: [condJson("Permanent")], effects: [effectJson("SkillStatChange", { magnitude: 0.1, skillStat: "HealDoubleChance" })] }),
    pdef("maria_tree2_tier1_passive1", { conditions: [condJson("Permanent")], effects: [effectJson("SkillStatChange", { magnitude: 0.1, skillId: "maria_innate_active4", skillStat: "Accuracy" })] }),
    pdef("maria_tree2_tier1_passive2", { conditions: [condJson("UponDealingDamage", "AnyBuff"), condJson("UponAllyDealingDamage", "AnyBuff")], effects: [effectJson("Heal", { magnitude: 3, chance: 0.25, target: "SelfAndAlly" })] }),
    pdef("maria_tree2_tier1_passive3", { conditions: [condJson("UponApplyingStatus", "AnyStatus")], effects: [effectJson("TokenManipulation", { token: "Strength", stacks: 1, target: "Opposite" })] }),
    pdef("maria_tree2_tier2_passive1", { conditions: [condJson("UponHealing")], effects: [effectJson("TokenManipulation", { token: "Strength", stacks: 1, target: "Opposite" })] }),
    pdef("maria_tree2_tier2_passive2", { conditions: [condJson("UponApplyingStatus", "AnyStatus")], effects: [effectJson("TokenManipulation", { token: "Defense", stacks: 1, chance: 0.7, target: "Opposite" })] }),
    pdef("maria_tree2_tier2_passive3", { conditions: [condJson("Permanent")], effects: [effectJson("SkillStatChange", { magnitude: 0.2, skillId: "maria_innate_active3", skillStat: "Accuracy" })] }),
    pdef("maria_tree2_tier3_passive1", { conditions: [condJson("UponDealingDamage")], effects: [effectJson("TokenManipulation", { token: "Dexterity", stacks: 1, target: "SelfAndAlly" })] }),
    pdef("maria_tree2_tier3_passive2", { conditions: [condJson("WhileHavingStatus", "AnyBuff")], effects: [effectJson("CharacterStatChange", { magnitude: 0.1, characterStat: "DefenseChance", target: "SelfAndAlly" })] }),
    pdef("maria_tree2_tier3_passive3", { conditions: [condJson("WhileHavingStatus", "AnyBuff")], effects: [effectJson("CharacterStatChange", { magnitude: 0.5, characterStat: "CriticalDamage", target: "SelfAndAlly" })] }),
    pdef("maria_tree3_tier1_passive1", { conditions: [condJson("UponApplyingStatus", "AnyDebuff")], effects: [effectJson("DealDamage", { magnitude: 1, target: "Opposite", scaleSrc: 1, scaleDiv: 1 })] }),
    pdef("maria_tree3_tier1_passive2", { conditions: [condJson("UponHittingTargetWithStatus", "AnyDebuff")], effects: [effectJson("Heal", { magnitude: 6, chance: 0.5, target: "Self" })] }),
    pdef("maria_tree3_tier1_passive3", { conditions: [condJson("WhileOppositeHasStatus", "AnyDebuff")], effects: [effectJson("CharacterStatChange", { magnitude: 0.1, characterStat: "DefenseChance" })] }),
    pdef("maria_tree3_tier2_passive1", { conditions: [condJson("UponHittingTargetWithStatus", undefined, "maria_innate_active1")], effects: [effectJson("TokenManipulation", { token: "Weaken", stacks: 2, chance: 0.4, target: "Opposite" })] }),
    pdef("maria_tree3_tier2_passive2", { conditions: ["maria_innate_active2", "maria_tree1_tier1_active", "maria_tree1_tier2_active", "maria_tree1_tier3_active"].map((skillId) => condJson("UponUsingSkill", undefined, skillId)), effects: [effectJson("TokenManipulation", { token: "Weaken", stacks: 1, chance: 0.4, target: "AllOpposites" })] }),
    pdef("maria_tree3_tier2_passive3", { conditions: ["maria_innate_active4", "maria_tree2_tier1_active", "maria_tree2_tier2_active", "maria_tree2_tier3_active"].map((skillId) => condJson("UponUsingSkill", undefined, skillId)), effects: [effectJson("TokenManipulation", { token: "Vulnerability", stacks: 1, chance: 0.4, target: "AllOpposites" })] }),
    pdef("maria_tree3_tier3_passive1", { conditions: [condJson("UponDamageTaken"), condJson("UponAllyDamageTaken")], effects: [effectJson("TokenManipulation", { token: "Weaken", stacks: 1, target: "Opposite" })] }),
    pdef("maria_tree3_tier3_passive2", { conditions: [condJson("Permanent")], effects: ["Weaken", "Vulnerability", "Clumsy", "Confusion", "Exposition", "Corrosion", "Mark", "Bleeding"].map((token) => effectJson("TokenStatChange", { magnitude: 0.5, token, target: "Opposite" })) }),
    pdef("maria_tree3_tier3_passive3", { conditions: [condJson("UponDamageTaken"), condJson("UponAllyDamageTaken")], effects: [effectJson("TokenManipulation", { token: "Vulnerability", stacks: 1, target: "Opposite" })] }),
  );
  return passives;
}

function treeNodes(tree) {
  return [1, 2, 3].map((tier) => ({
    tier,
    nodes: [
      ...[1, 2, 3].map((index) => ({ id: `maria_tree${tree}_tier${tier}_passive${index}`, type: "Passive", cost: 1, requires: [] })),
      { id: `maria_tree${tree}_tier${tier}_active`, type: "Active", cost: 1, requires: [1, 2, 3].map((index) => `maria_tree${tree}_tier${tier}_passive${index}`) },
    ],
  }));
}

function patchJson() {
  const skillsPath = path.join(DATA_DIR, "skills.json");
  const skills = JSON.parse(fs.readFileSync(skillsPath, "utf8")).filter((skill) => !OLD_SKILL_IDS.has(skill.id));
  skills.push(...mariaSkills());
  fs.writeFileSync(skillsPath, `${JSON.stringify(skills, null, 2)}\n`);

  const passivesPath = path.join(DATA_DIR, "passives.json");
  const passives = JSON.parse(fs.readFileSync(passivesPath, "utf8")).filter((passive) => !passive.id.startsWith("m_lf_") && !passive.id.startsWith("m_bh_") && !passive.id.startsWith("m_ds_"));
  passives.push(...mariaPassives());
  fs.writeFileSync(passivesPath, `${JSON.stringify(passives, null, 2)}\n`);

  const treesPath = path.join(DATA_DIR, "skill_trees.json");
  const trees = JSON.parse(fs.readFileSync(treesPath, "utf8"));
  const mariaIndex = trees.findIndex((character) => character.characterId === "maria");
  trees[mariaIndex] = {
    characterId: "maria",
    trees: [
      { element: "Fire", tiers: treeNodes(1) },
      { element: "Metal", tiers: treeNodes(2) },
      { element: "Anomaly", tiers: treeNodes(3) },
    ],
  };
  fs.writeFileSync(treesPath, `${JSON.stringify(trees, null, 2)}\n`);
}

function writeKit(abilityGuids) {
  const kitGuid = newGuid();
  const refs = abilityGuids.map((guid) => `  - {fileID: 11400000, guid: ${guid}, type: 2}`).join("\n");
  fs.writeFileSync(path.join(MARIA_DIR, "MariaCombatKit.asset"), yamlHeader("MariaCombatKit", KIT_SCRIPT_GUID) + `  _characterId: maria\n  _abilities:\n${refs}\n`);
  fs.writeFileSync(path.join(MARIA_DIR, "MariaCombatKit.asset.meta"), metaText(kitGuid));
}

function writeAllyStats() {
  const guid = newGuid();
  const text = `%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: ${ALLY_SCRIPT_GUID}, type: 3}
  m_Name: MariaAllyCharacterStatDefinition
  m_EditorClassIdentifier: Assembly-CSharp::Erumperem.Characters.AllyCharacterStatDefinition
  characterId: Maria
  displayName: The Star
  defaultExplorationState: 3
  maxHitPoints: 70
  speed: 6
  accuracy: 1
  critChance: 0.01
  defenseChance: 0.12
  burnResistance: 0.1
  blightResistance: 0.1
  stunResistance: 0.1
  elementType: 3
  progressionCharacterId: maria
  battlePrefab: {fileID: 0}
  battleFormationRank: 3
`;
  fs.writeFileSync(path.join(CHAR_DIR, "MariaAllyCharacterStatDefinition.asset"), text);
  fs.writeFileSync(path.join(CHAR_DIR, "MariaAllyCharacterStatDefinition.asset.meta"), metaText(guid));
  return guid;
}

function patchAllyCatalog(mariaGuid) {
  const catalog = path.join(CHAR_DIR, "AllyCharacterStatCatalog.asset");
  let text = fs.readFileSync(catalog, "utf8");
  if (!text.includes(mariaGuid)) {
    text = `${text.trimEnd()}\n  - {fileID: 11400000, guid: ${mariaGuid}, type: 2}\n`;
    fs.writeFileSync(catalog, text);
  }
}

function remapNodes() {
  for (const [oldId, newId] of Object.entries(NODE_ID_REMAP)) {
    const asset = path.join(NODES_DIR, `${oldId}.asset`);
    if (!fs.existsSync(asset)) continue;
    const text = fs.readFileSync(asset, "utf8").replace(`  _nodeId: ${oldId}`, `  _nodeId: ${newId}`);
    fs.writeFileSync(asset, text);
  }
}

function remapVisuals() {
  let text = fs.readFileSync(VISUAL_ASSET, "utf8");
  for (const [oldId, [newId, display]] of Object.entries(VISUAL_REMAP)) {
    text = text.replace(`skillId: ${oldId}\n    displayName: ${oldId}`, `skillId: ${newId}\n    displayName: ${display}`);
    text = text.replaceAll(`skillId: ${oldId}`, `skillId: ${newId}`);
  }
  fs.writeFileSync(VISUAL_ASSET, text);
}

function patchCatalogFallback() {
  const catalogCs = path.join(ROOT, "Assets/_Project/Scripts/Characters/AllyCharacterStatCatalog.cs");
  let text = fs.readFileSync(catalogCs, "utf8");
  if (!text.includes('"Maria"')) {
    text = text.replace('"Matsuda" => 100f,', '"Matsuda" => 100f,\n                "Maria" => 70f,');
    fs.writeFileSync(catalogCs, text);
  }
}

const specs = buildSpecs();
const guids = specs.map(() => newGuid());
specs.forEach((spec, index) => writeAbility(spec, guids[index]));
writeKit(guids);
const mariaGuid = writeAllyStats();
patchAllyCatalog(mariaGuid);
patchCatalogFallback();
patchJson();
remapNodes();
remapVisuals();
console.log(`Wrote ${specs.length} Maria abilities.`);
