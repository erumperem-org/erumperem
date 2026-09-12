import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { randomUUID } from "node:crypto";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = join(dirname(fileURLToPath(import.meta.url)), "..");
const ABILITY_DIR = join(ROOT, "Assets/_Project/ScriptableObjects/Combat/Buck");
const ABILITY_SCRIPT = "c0ab11117e8f4d2e9a3b5c6d7e8f9012";
const KIT_SCRIPT = "f3de4444a1bc4b5b2d6e8f9012345678";
const STREAMING = join(ROOT, "Assets/StreamingAssets/Data");
const NODES = join(ROOT, "Assets/_Project/Resources/SkillTreeNodes");

const REVOLVER = ["buck_innate_active1", "buck_innate_active3"];
const PISTOL = ["buck_innate_active2", "buck_tree3_tier2_active"];
const RIFLE = ["buck_innate_active4"];
const BASIC = "buck_innate_active1";

const LuckyShot = 16, Vulnerability = 13, Strength = 10, Dexterity = 17, Exposition = 18, Corrosion = 19, Mark = 20;
const Permanent = 0, UponDamageTaken = 2, UponCriticalStrike = 4, UponHittingTargetWithStatus = 9, UponDealingDamage = 10, UponUsingSkill = 14, WhileOppositeHasStatus = 13;
const CharacterStatChange = 0, SkillStatChange = 1, TokenStatChange = 2, ExtraStatsFromResource = 3, TokenManipulation = 4, TurnManipulation = 5;
const DamageCaused = 4, Accuracy = 5, CriticalChance = 6, CriticalDamage = 7, SkillDamage = 1, SkillAccuracy = 2, SkillHitCount = 6, SkillChanceToNotEndTurn = 7, EnemiesDefeatedThisBattle = 7, Self = 0, Opposite = 3, LeaderRole = 1, CompanionRole = 2;

const TREE_NODE_MAP = {
  b_ar_t1_p1: ["buck_tree1_tier1_passive1", "Accuracy +15%"],
  b_ar_t1_p2: ["buck_tree1_tier1_passive2", "Revolver attacks +2 damage"],
  b_ar_t1_p3: ["buck_tree1_tier1_passive3", "Unload Ammo +1 bullet"],
  buckSpiderHands: ["buck_tree1_tier1_active", "More hands to aim with"],
  b_ar_t2_p1: ["buck_tree1_tier2_passive1", "Strength on Vulnerability damage"],
  b_ar_t2_p2: ["buck_tree1_tier2_passive2", "Dexterity on Vulnerability damage"],
  b_ar_t2_p3: ["buck_tree1_tier2_passive3", "Revolver attacks +3 damage"],
  buckAllGuns: ["buck_tree1_tier2_active", "Guns for all"],
  b_ar_t3_p1: ["buck_tree1_tier3_passive1", "Rifle 50% chance to not end turn"],
  b_ar_t3_p2: ["buck_tree1_tier3_passive2", "First basic +25% not end turn"],
  b_ar_t3_p3: ["buck_tree1_tier3_passive3", "First skill +25% not end turn"],
  buckJuggle: ["buck_tree1_tier3_active", "Juggling"],
  b_sn_t1_p1: ["buck_tree2_tier1_passive1", "Rifle accuracy +20%"],
  b_sn_t1_p2: ["buck_tree2_tier1_passive2", "Pistol accuracy +25%"],
  b_sn_t1_p3: ["buck_tree2_tier1_passive3", "Rifle attacks +5 damage"],
  buckSnakeVision: ["buck_tree2_tier1_active", "Heat Vision"],
  b_sn_t2_p1: ["buck_tree2_tier2_passive1", "Revolver chance to apply Vulnerability"],
  b_sn_t2_p2: ["buck_tree2_tier2_passive2", "Pistol chance to apply Vulnerability"],
  b_sn_t2_p3: ["buck_tree2_tier2_passive3", "+20% accuracy vs Vulnerability"],
  buckSnakeBite: ["buck_tree2_tier2_active", "Corrosive bite"],
  b_sn_t3_p1: ["buck_tree2_tier3_passive1", "Vulnerability you apply +50% effective"],
  b_sn_t3_p2: ["buck_tree2_tier3_passive2", "Exposition you apply +50% effective"],
  b_sn_t3_p3: ["buck_tree2_tier3_passive3", "Hits 10% apply Corrosion"],
  buckSnakeTail: ["buck_tree2_tier3_active", "Strangle"],
  b_du_t1_p1: ["buck_tree3_tier1_passive1", "Pistol attacks +50% damage"],
  b_du_t1_p2: ["buck_tree3_tier1_passive2", "Pistol crits +100% damage"],
  b_du_t1_p3: ["buck_tree3_tier1_passive3", "+3% crit vs Vulnerability"],
  buckMark: ["buck_tree3_tier1_active", "Challenge for Duel"],
  b_du_t2_p1: ["buck_tree3_tier2_passive1", "Revolver +25% vs Mark"],
  b_du_t2_p2: ["buck_tree3_tier2_passive2", "Rifle +75% vs Mark"],
  b_du_t2_p3: ["buck_tree3_tier2_passive3", "Hits vs Vulnerability 10% apply Mark"],
  buckPistolHeadShot: ["buck_tree3_tier2_active", "Aim for the head"],
  b_du_t3_p1: ["buck_tree3_tier3_passive1", "Attackers 25% receive Mark"],
  b_du_t3_p2: ["buck_tree3_tier3_passive2", "Pistol +50% damage per defeated enemy"],
  b_du_t3_p3: ["buck_tree3_tier3_passive3", "Marks you apply +100% effective"],
  buckLuckManipulation: ["buck_tree3_tier3_active", "Lucky ammunition"],
};

const OLD_SKILL_ORDER = [
  "buckBasicHit", "buckPistol", "buckRevolver", "buckRifle",
  "buckSpiderHands", "buckAllGuns", "buckJuggle",
  "buckSnakeVision", "buckSnakeBite", "buckSnakeTail",
  "buckMark", "buckPistolHeadShot", "buckLuckManipulation",
];

function guid() {
  return randomUUID().replaceAll("-", "");
}

function writeMeta(path, id) {
  writeFileSync(path, `fileFormatVersion: 2\nguid: ${id}\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
}

function yamlHeader(name) {
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
  m_Script: {fileID: 11500000, guid: ${ABILITY_SCRIPT}, type: 3}
  m_Name: ${name}
  m_EditorClassIdentifier: 
`;
}

function identity(abilityId, displayName, kind, placement, tree = 1, tier = 1, passiveIndex = 1, corruptionMin = 0) {
  return `  _abilityId: ${abilityId}
  _displayName: ${displayName}
  _ownerCharacterId: buck
  _abilityKind: ${kind}
  _placement: ${placement}
  _treeIndex: ${tree}
  _tierIndex: ${tier}
  _passiveIndex: ${passiveIndex}
  _corruptionMinTier: ${corruptionMin}
  _designerNotes: Buck kit. Duplicate this asset, change abilityId, Export Catalog.
  _activeSkillTypeLabel: Active
`;
}

function tokenEffect(token, stacks) {
  return `  - Type: 0
    Chance: 1
    Stacks: ${stacks}
    Potency: 0
    AmountMax: 0
    Duration: 0
    Steps: 0
    EffectScope: 0
    HasToken: 1
    Token: ${token}
    HasDot: 0
    Dot: 0
    HasScaleFromToken: 0
    ScaleFromToken: 0
    ScaleStacksPerSourceStack: 0
    ScaleStacksSourceDivisor: 1
`;
}

function applyTokens(...pairs) {
  return "  _effectsAppliedAfterSuccessfulHit:\n" + pairs.map(([token, stacks]) => tokenEffect(token, stacks)).join("");
}

function treePrereqs(tree, tier) {
  return [1, 2, 3].map((index) => `buck_tree${tree}_tier${tier}_passive${index}`);
}

function activeCore({
  element, dmin, dmax, crit, acc, target, corruption, hitCount = 1, chanceNotEnd = 0, followUps = [],
  grantsBonus = 0, accPenalty = 0, computeDebuff = 0, dmgPerDebuff = 0, critPerDebuff = 0, accPerDebuff = 0,
  effectsYaml = "  _effectsAppliedAfterSuccessfulHit: []\n", prereqs = [],
}) {
  const follow = followUps.length
    ? "  _followUpSkillIds:\n" + followUps.map((id) => `  - ${id}\n`).join("")
    : "  _followUpSkillIds: []\n";
  const prereq = prereqs.length
    ? "  _treeNodePrerequisiteAbilityIds:\n" + prereqs.map((id) => `  - ${id}\n`).join("")
    : "  _treeNodePrerequisiteAbilityIds: []\n";
  return `  _activeSkillDamageElement: ${element}
  _baseDamageMinimum: ${dmin}
  _baseDamageMaximum: ${dmax}
  _baseCriticalHitChanceFraction: ${crit}
  _baseHitAccuracyFraction: ${acc}
  _targetSelectionKind: ${target}
  _aiAbsoluteChanceToConsiderWhenEligible: 1
  _aiOnlyEligibleWhenOwnHpFractionBelow: 1
  _corruptionCostAddedWhenPlayerCasts: ${corruption}
  _hitCount: ${hitCount}
  _chanceToNotEndTurn: ${chanceNotEnd}
${follow}  _grantsBonusActionsToAllies: ${grantsBonus}
  _accuracyPenaltyPerLivingEnemy: ${accPenalty}
  _hasBonusDamagePerOwnToken: 0
  _bonusDamagePerOwnToken: 0
  _bonusDamagePerOwnTokenStacks: 1
  _computeFromDebuffTypesOnTarget: ${computeDebuff}
  _damagePerDistinctDebuffType: ${dmgPerDebuff}
  _critChancePerDistinctDebuffType: ${critPerDebuff}
  _accuracyPerDistinctDebuffType: ${accPerDebuff}
  _canTargetDeadAllies: 0
${effectsYaml}  _requiredPartyRole: 0
  _chanceToTrigger: 1
  _maxTriggersPerBattle: 0
  _maxTriggersPerTurn: 0
  _passiveConditions: []
  _passiveEffects: []
  _treeNodeUnlockCost: 1
${prereq}`;
}

function condYaml(activation, skillId = "", hasStatus = 0, status = 0) {
  return `  - Activation: ${activation}
    HasRequiredStatus: ${hasStatus}
    RequiredStatus: ${status}
    StatusMatch: 0
    HitPointsLostPerTrigger: 0
    StatThresholdFraction: 0
    StatThresholdComparison: 0
    SkillId: ${skillId}
`;
}

function effectYaml({
  operation, magnitude = 0, hasToken = 0, token = 0, skillId = "", stacks = 0, chance = 1, maxBattle = 0,
  characterStat = 0, skillStat = 0, resource = 0, target = 0,
}) {
  return `  - Operation: ${operation}
    Magnitude: ${magnitude}
    HasToken: ${hasToken}
    Token: ${token}
    SkillId: ${skillId}
    SummonEnemyId: 
    Stacks: ${stacks}
    ChanceToTrigger: ${chance}
    MaxTriggersPerBattle: ${maxBattle}
    MaxTriggersPerTurn: 0
    CharacterStat: ${characterStat}
    SkillStat: ${skillStat}
    Resource: ${resource}
    StatChangeTarget: ${target}
    TokenManipulationMode: 0
    HasResourceToken: 0
    ResourceToken: 0
    ScaleStacksPerSourceStack: 0
    ScaleStacksSourceDivisor: 0
    ExpiresAtEndOfOpposingSideTurn: 0
    SkipEndOfTurnDecayChance: 0
`;
}

function skillStatEffects(skillIds, skillStat, magnitude) {
  return skillIds.map((skillId) => effectYaml({ operation: SkillStatChange, magnitude, skillId, skillStat })).join("");
}

function skillIdConditions(activation, skillIds, hasStatus = 0, status = 0) {
  return skillIds.map((skillId) => condYaml(activation, skillId, hasStatus, status)).join("");
}

function passiveAsset(abilityId, displayName, placement, tree, tier, index, conditions, effects, role = 0, corruptionMin = 0, chance = 1, maxBattle = 0) {
  return yamlHeader(abilityId) + identity(abilityId, displayName, 1, placement, tree, tier, index, corruptionMin) +
`  _activeSkillDamageElement: 0
  _baseDamageMinimum: 0
  _baseDamageMaximum: 0
  _baseCriticalHitChanceFraction: 0
  _baseHitAccuracyFraction: 1
  _targetSelectionKind: 0
  _aiAbsoluteChanceToConsiderWhenEligible: 1
  _aiOnlyEligibleWhenOwnHpFractionBelow: 1
  _corruptionCostAddedWhenPlayerCasts: 0
  _hitCount: 1
  _effectsAppliedAfterSuccessfulHit: []
  _requiredPartyRole: ${role}
  _chanceToTrigger: ${chance}
  _maxTriggersPerBattle: ${maxBattle}
  _maxTriggersPerTurn: 0
  _passiveConditions:
${conditions}  _passiveEffects:
${effects}  _treeNodeUnlockCost: 1
  _treeNodePrerequisiteAbilityIds: []
`;
}

function jsonCond(activation, skillId, status) {
  const item = { activation };
  if (status) item.requiredStatus = status;
  if (skillId) item.skillId = skillId;
  return item;
}

function jsonEffect(operation, extra = {}) {
  const item = { chanceToTrigger: extra.chance ?? 1, operation };
  for (const [key, value] of Object.entries(extra)) {
    if (key !== "chance" && value !== undefined) item[key] = value;
  }
  if (operation === "TokenManipulation") item.tokenManipulationMode = "Apply";
  return item;
}

function jsonPassive(id, conditions, effects, extra = {}) {
  const item = {
    id,
    requiredPartyRole: extra.role ?? "Any",
    corruptionMinTier: extra.corruptionMin ?? 0,
    chanceToTrigger: extra.chance ?? 1,
    conditions,
    effects,
  };
  if (extra.maxBattle) item.maxTriggersPerBattle = extra.maxBattle;
  return item;
}

function jsonSkillStats(skillIds, skillStat, magnitude) {
  return skillIds.map((skillId) => jsonEffect("SkillStatChange", { magnitude, skillId, skillStat }));
}

function jsonSkillConds(activation, skillIds, status) {
  return skillIds.map((skillId) => jsonCond(activation, skillId, status));
}

function buildPassivesJson() {
  const passives = [
    jsonPassive("buck_leader_passive1", [jsonCond("UponCriticalStrike")], [jsonEffect("CharacterStatChange", { magnitude: 0.5, characterStat: "CriticalDamage", statChangeTarget: "Self" })], { role: "Leader" }),
    jsonPassive("buck_companion_passive1", [jsonCond("UponHittingTargetWithStatus", BASIC)], [jsonEffect("TokenManipulation", { token: "LuckyShot", stacks: 2, chance: 0.5, statChangeTarget: "Self" })], { role: "Companion" }),
  ];
  for (const [tier, acc, crit] of [[1, -0.05, 0.03], [2, -0.08, 0.05], [3, -0.10, 0.08]]) {
    passives.push(jsonPassive(`buck_corruption_tier${tier}_passive1`, [jsonCond("Permanent")], [
      jsonEffect("CharacterStatChange", { magnitude: acc, characterStat: "Accuracy" }),
      jsonEffect("CharacterStatChange", { magnitude: crit, characterStat: "CriticalChance" }),
    ], { corruptionMin: tier }));
  }
  const tree = (treeIndex, tier, index, conditions, effects, extra) => {
    passives.push(jsonPassive(`buck_tree${treeIndex}_tier${tier}_passive${index}`, conditions, effects, extra));
  };
  tree(1, 1, 1, [jsonCond("Permanent")], [jsonEffect("CharacterStatChange", { magnitude: 0.15, characterStat: "Accuracy" })]);
  tree(1, 1, 2, [jsonCond("Permanent")], jsonSkillStats(REVOLVER, "Damage", 2));
  tree(1, 1, 3, [jsonCond("Permanent")], jsonSkillStats(["buck_innate_active3"], "HitCount", 1));
  tree(1, 2, 1, [jsonCond("UponDealingDamage", undefined, "Vulnerability")], [jsonEffect("TokenManipulation", { token: "Strength", stacks: 1, statChangeTarget: "Self" })]);
  tree(1, 2, 2, [jsonCond("UponDealingDamage", undefined, "Vulnerability")], [jsonEffect("TokenManipulation", { token: "Dexterity", stacks: 1, statChangeTarget: "Self" })]);
  tree(1, 2, 3, [jsonCond("Permanent")], jsonSkillStats(REVOLVER, "Damage", 3));
  tree(1, 3, 1, [jsonCond("Permanent")], jsonSkillStats(RIFLE, "ChanceToNotEndTurn", 0.5));
  tree(1, 3, 2, [jsonCond("UponUsingSkill", BASIC)], [jsonEffect("TurnManipulation", { stacks: 1, chance: 0.25, maxTriggersPerBattle: 1 })], { maxBattle: 1 });
  tree(1, 3, 3, [jsonCond("UponUsingSkill")], [jsonEffect("TurnManipulation", { stacks: 1, chance: 0.25, maxTriggersPerBattle: 1 })], { maxBattle: 1 });
  tree(2, 1, 1, [jsonCond("Permanent")], jsonSkillStats(RIFLE, "Accuracy", 0.20));
  tree(2, 1, 2, [jsonCond("Permanent")], jsonSkillStats(PISTOL, "Accuracy", 0.25));
  tree(2, 1, 3, [jsonCond("Permanent")], jsonSkillStats(RIFLE, "Damage", 5));
  tree(2, 2, 1, jsonSkillConds("UponHittingTargetWithStatus", REVOLVER), [jsonEffect("TokenManipulation", { token: "Vulnerability", stacks: 1, chance: 0.15, statChangeTarget: "Opposite" })]);
  tree(2, 2, 2, jsonSkillConds("UponHittingTargetWithStatus", PISTOL), [jsonEffect("TokenManipulation", { token: "Vulnerability", stacks: 2, chance: 0.20, statChangeTarget: "Opposite" })]);
  tree(2, 2, 3, [jsonCond("WhileOppositeHasStatus", undefined, "Vulnerability")], [jsonEffect("CharacterStatChange", { magnitude: 0.20, characterStat: "Accuracy" })]);
  tree(2, 3, 1, [jsonCond("Permanent")], [jsonEffect("TokenStatChange", { magnitude: 0.50, token: "Vulnerability", statChangeTarget: "Opposite" })]);
  tree(2, 3, 2, [jsonCond("Permanent")], [jsonEffect("TokenStatChange", { magnitude: 0.50, token: "Exposition", statChangeTarget: "Opposite" })]);
  tree(2, 3, 3, [jsonCond("UponHittingTargetWithStatus")], [jsonEffect("TokenManipulation", { token: "Corrosion", stacks: 2, chance: 0.10, statChangeTarget: "Opposite" })]);
  tree(3, 1, 1, jsonSkillConds("Permanent", PISTOL), [jsonEffect("CharacterStatChange", { magnitude: 0.50, characterStat: "DamageCaused" })]);
  tree(3, 1, 2, jsonSkillConds("Permanent", PISTOL), [jsonEffect("CharacterStatChange", { magnitude: 1.0, characterStat: "CriticalDamage" })]);
  tree(3, 1, 3, [jsonCond("WhileOppositeHasStatus", undefined, "Vulnerability")], [jsonEffect("CharacterStatChange", { magnitude: 0.03, characterStat: "CriticalChance" })]);
  tree(3, 2, 1, jsonSkillConds("WhileOppositeHasStatus", REVOLVER, "Mark"), [jsonEffect("CharacterStatChange", { magnitude: 0.25, characterStat: "DamageCaused" })]);
  tree(3, 2, 2, jsonSkillConds("WhileOppositeHasStatus", RIFLE, "Mark"), [jsonEffect("CharacterStatChange", { magnitude: 0.75, characterStat: "DamageCaused" })]);
  tree(3, 2, 3, [jsonCond("UponHittingTargetWithStatus", undefined, "Vulnerability")], [jsonEffect("TokenManipulation", { token: "Mark", stacks: 1, chance: 0.10, statChangeTarget: "Opposite" })]);
  tree(3, 3, 1, [jsonCond("UponDamageTaken")], [jsonEffect("TokenManipulation", { token: "Mark", stacks: 2, chance: 0.25, statChangeTarget: "Opposite" })]);
  tree(3, 3, 2, jsonSkillConds("Permanent", PISTOL), [jsonEffect("ExtraStatsFromResource", { magnitude: 0.50, resource: "EnemiesDefeatedThisBattle", characterStat: "DamageCaused" })]);
  tree(3, 3, 3, [jsonCond("Permanent")], [jsonEffect("TokenStatChange", { magnitude: 1.0, token: "Mark", statChangeTarget: "Opposite" })]);
  return passives;
}

function addActive(list, abilityId, display, placement, tree, tier, body) {
  list.push([abilityId, yamlHeader(abilityId) + identity(abilityId, display, 0, placement, tree, tier) + body]);
}

function buildActives() {
  const actives = [];
  addActive(actives, "buck_innate_active1", "revolver shot", 0, 1, 1, activeCore({ element: 1, dmin: 8, dmax: 13, crit: 0.05, acc: 0.55, target: 0, corruption: 0 }));
  addActive(actives, "buck_innate_active2", "Pistol Draw", 0, 1, 1, activeCore({ element: 1, dmin: 4, dmax: 8, crit: 0.07, acc: 0.5, target: 3, corruption: 2, chanceNotEnd: 0.75 }));
  addActive(actives, "buck_innate_active3", "Unload ammo", 0, 1, 1, activeCore({ element: 1, dmin: 5, dmax: 10, crit: 0.06, acc: 0.4, target: 0, corruption: 3, hitCount: 3 }));
  addActive(actives, "buck_innate_active4", "Hunting Rifle", 0, 1, 1, activeCore({ element: 3, dmin: 15, dmax: 25, crit: 0.05, acc: 0.8, target: 0, corruption: 4, effectsYaml: applyTokens([Vulnerability, 3]) }));
  addActive(actives, "buck_tree1_tier1_active", "More hands to aim with", 1, 1, 1, activeCore({ element: 3, dmin: 0, dmax: 0, crit: 0, acc: 1, target: 2, corruption: 4, effectsYaml: applyTokens([Strength, 3], [Dexterity, 3]), prereqs: treePrereqs(1, 1) }));
  addActive(actives, "buck_tree1_tier2_active", "Guns for all", 1, 1, 2, activeCore({ element: 3, dmin: 0, dmax: 0, crit: 0, acc: 0, target: 0, corruption: 6, followUps: ["buck_innate_active2", "buck_innate_active3", "buck_innate_active4"], prereqs: treePrereqs(1, 2) }));
  addActive(actives, "buck_tree1_tier3_active", "Juggling", 1, 1, 3, activeCore({ element: 3, dmin: 0, dmax: 0, crit: 0, acc: 1, target: 2, corruption: 5, grantsBonus: 1, accPenalty: 0.1, prereqs: treePrereqs(1, 3) }));
  addActive(actives, "buck_tree2_tier1_active", "Heat Vision", 1, 2, 1, activeCore({ element: 3, dmin: 0, dmax: 0, crit: 0, acc: 2, target: 0, corruption: 4, effectsYaml: applyTokens([Vulnerability, 3], [Exposition, 3]), prereqs: treePrereqs(2, 1) }));
  addActive(actives, "buck_tree2_tier2_active", "Corrosive bite", 1, 2, 2, activeCore({ element: 3, dmin: 3, dmax: 8, crit: 0.03, acc: 0.75, target: 0, corruption: 3, effectsYaml: applyTokens([Corrosion, 3]), prereqs: treePrereqs(2, 2) }));
  addActive(actives, "buck_tree2_tier3_active", "Strangle", 1, 2, 3, activeCore({ element: 3, dmin: 0, dmax: 0, crit: 0, acc: 0, target: 0, corruption: 5, computeDebuff: 1, dmgPerDebuff: 10, critPerDebuff: 0.04, accPerDebuff: 0.3, prereqs: treePrereqs(2, 3) }));
  addActive(actives, "buck_tree3_tier1_active", "Challenge for Duel", 1, 3, 1, activeCore({ element: 1, dmin: 0, dmax: 0, crit: 0, acc: 0.8, target: 0, corruption: 3, effectsYaml: applyTokens([Mark, 3]), prereqs: treePrereqs(3, 1) }));
  addActive(actives, "buck_tree3_tier2_active", "Aim for the head", 1, 3, 2, activeCore({ element: 1, dmin: 4, dmax: 8, crit: 0.25, acc: 0.4, target: 0, corruption: 5, hitCount: 3, chanceNotEnd: 0.9, prereqs: treePrereqs(3, 2) }));
  addActive(actives, "buck_tree3_tier3_active", "Lucky ammunition", 1, 3, 3, activeCore({ element: 3, dmin: 0, dmax: 0, crit: 0, acc: 1, target: 2, corruption: 6, effectsYaml: applyTokens([LuckyShot, 6]), prereqs: treePrereqs(3, 3) }));
  return actives;
}

function addPassive(list, abilityId, display, placement, tree, tier, index, conditions, effects, role = 0, corruptionMin = 0, chance = 1, maxBattle = 0) {
  list.push([abilityId, passiveAsset(abilityId, display, placement, tree, tier, index, conditions, effects, role, corruptionMin, chance, maxBattle)]);
}

function buildPassiveAssets() {
  const assets = [];
  addPassive(assets, "buck_leader_passive1", "Leader: crit damage per crit this battle", 2, 1, 1, 1, condYaml(UponCriticalStrike), effectYaml({ operation: CharacterStatChange, magnitude: 0.5, characterStat: CriticalDamage, target: Self }), LeaderRole);
  addPassive(assets, "buck_companion_passive1", "Companion: basic attacks grant Lucky Shot", 3, 1, 1, 1, condYaml(UponHittingTargetWithStatus, BASIC), effectYaml({ operation: TokenManipulation, hasToken: 1, token: LuckyShot, stacks: 2, chance: 0.5, target: Self }), CompanionRole);
  for (const [tier, acc, crit] of [[1, -0.05, 0.03], [2, -0.08, 0.05], [3, -0.10, 0.08]]) {
    addPassive(assets, `buck_corruption_tier${tier}_passive1`, `Corruption T${tier}: accuracy and crit`, 4, 1, 1, 1, condYaml(Permanent), effectYaml({ operation: CharacterStatChange, magnitude: acc, characterStat: Accuracy }) + effectYaml({ operation: CharacterStatChange, magnitude: crit, characterStat: CriticalChance }), 0, tier);
  }
  addPassive(assets, "buck_tree1_tier1_passive1", "Accuracy +15%", 1, 1, 1, 1, condYaml(Permanent), effectYaml({ operation: CharacterStatChange, magnitude: 0.15, characterStat: Accuracy }));
  addPassive(assets, "buck_tree1_tier1_passive2", "Revolver attacks +2 damage", 1, 1, 1, 2, condYaml(Permanent), skillStatEffects(REVOLVER, SkillDamage, 2));
  addPassive(assets, "buck_tree1_tier1_passive3", "Unload Ammo +1 bullet", 1, 1, 1, 3, condYaml(Permanent), skillStatEffects(["buck_innate_active3"], SkillHitCount, 1));
  addPassive(assets, "buck_tree1_tier2_passive1", "Strength on Vulnerability damage", 1, 1, 2, 1, condYaml(UponDealingDamage, "", 1, Vulnerability), effectYaml({ operation: TokenManipulation, hasToken: 1, token: Strength, stacks: 1, target: Self }));
  addPassive(assets, "buck_tree1_tier2_passive2", "Dexterity on Vulnerability damage", 1, 1, 2, 2, condYaml(UponDealingDamage, "", 1, Vulnerability), effectYaml({ operation: TokenManipulation, hasToken: 1, token: Dexterity, stacks: 1, target: Self }));
  addPassive(assets, "buck_tree1_tier2_passive3", "Revolver attacks +3 damage", 1, 1, 2, 3, condYaml(Permanent), skillStatEffects(REVOLVER, SkillDamage, 3));
  addPassive(assets, "buck_tree1_tier3_passive1", "Rifle 50% chance to not end turn", 1, 1, 3, 1, condYaml(Permanent), skillStatEffects(RIFLE, SkillChanceToNotEndTurn, 0.5));
  addPassive(assets, "buck_tree1_tier3_passive2", "First basic +25% not end turn", 1, 1, 3, 2, condYaml(UponUsingSkill, BASIC), effectYaml({ operation: TurnManipulation, stacks: 1, chance: 0.25, maxBattle: 1 }), 0, 0, 1, 1);
  addPassive(assets, "buck_tree1_tier3_passive3", "First skill +25% not end turn", 1, 1, 3, 3, condYaml(UponUsingSkill), effectYaml({ operation: TurnManipulation, stacks: 1, chance: 0.25, maxBattle: 1 }), 0, 0, 1, 1);
  addPassive(assets, "buck_tree2_tier1_passive1", "Rifle accuracy +20%", 1, 2, 1, 1, condYaml(Permanent), skillStatEffects(RIFLE, SkillAccuracy, 0.20));
  addPassive(assets, "buck_tree2_tier1_passive2", "Pistol accuracy +25%", 1, 2, 1, 2, condYaml(Permanent), skillStatEffects(PISTOL, SkillAccuracy, 0.25));
  addPassive(assets, "buck_tree2_tier1_passive3", "Rifle attacks +5 damage", 1, 2, 1, 3, condYaml(Permanent), skillStatEffects(RIFLE, SkillDamage, 5));
  addPassive(assets, "buck_tree2_tier2_passive1", "Revolver chance to apply Vulnerability", 1, 2, 2, 1, skillIdConditions(UponHittingTargetWithStatus, REVOLVER), effectYaml({ operation: TokenManipulation, hasToken: 1, token: Vulnerability, stacks: 1, chance: 0.15, target: Opposite }));
  addPassive(assets, "buck_tree2_tier2_passive2", "Pistol chance to apply Vulnerability", 1, 2, 2, 2, skillIdConditions(UponHittingTargetWithStatus, PISTOL), effectYaml({ operation: TokenManipulation, hasToken: 1, token: Vulnerability, stacks: 2, chance: 0.20, target: Opposite }));
  addPassive(assets, "buck_tree2_tier2_passive3", "+20% accuracy vs Vulnerability", 1, 2, 2, 3, condYaml(WhileOppositeHasStatus, "", 1, Vulnerability), effectYaml({ operation: CharacterStatChange, magnitude: 0.20, characterStat: Accuracy }));
  addPassive(assets, "buck_tree2_tier3_passive1", "Vulnerability you apply +50% effective", 1, 2, 3, 1, condYaml(Permanent), effectYaml({ operation: TokenStatChange, magnitude: 0.50, hasToken: 1, token: Vulnerability, target: Opposite }));
  addPassive(assets, "buck_tree2_tier3_passive2", "Exposition you apply +50% effective", 1, 2, 3, 2, condYaml(Permanent), effectYaml({ operation: TokenStatChange, magnitude: 0.50, hasToken: 1, token: Exposition, target: Opposite }));
  addPassive(assets, "buck_tree2_tier3_passive3", "Hits 10% apply Corrosion", 1, 2, 3, 3, condYaml(UponHittingTargetWithStatus), effectYaml({ operation: TokenManipulation, hasToken: 1, token: Corrosion, stacks: 2, chance: 0.10, target: Opposite }));
  addPassive(assets, "buck_tree3_tier1_passive1", "Pistol attacks +50% damage", 1, 3, 1, 1, skillIdConditions(Permanent, PISTOL), effectYaml({ operation: CharacterStatChange, magnitude: 0.50, characterStat: DamageCaused }));
  addPassive(assets, "buck_tree3_tier1_passive2", "Pistol crits +100% damage", 1, 3, 1, 2, skillIdConditions(Permanent, PISTOL), effectYaml({ operation: CharacterStatChange, magnitude: 1.0, characterStat: CriticalDamage }));
  addPassive(assets, "buck_tree3_tier1_passive3", "+3% crit vs Vulnerability", 1, 3, 1, 3, condYaml(WhileOppositeHasStatus, "", 1, Vulnerability), effectYaml({ operation: CharacterStatChange, magnitude: 0.03, characterStat: CriticalChance }));
  addPassive(assets, "buck_tree3_tier2_passive1", "Revolver +25% vs Mark", 1, 3, 2, 1, skillIdConditions(WhileOppositeHasStatus, REVOLVER, 1, Mark), effectYaml({ operation: CharacterStatChange, magnitude: 0.25, characterStat: DamageCaused }));
  addPassive(assets, "buck_tree3_tier2_passive2", "Rifle +75% vs Mark", 1, 3, 2, 2, skillIdConditions(WhileOppositeHasStatus, RIFLE, 1, Mark), effectYaml({ operation: CharacterStatChange, magnitude: 0.75, characterStat: DamageCaused }));
  addPassive(assets, "buck_tree3_tier2_passive3", "Hits vs Vulnerability 10% apply Mark", 1, 3, 2, 3, condYaml(UponHittingTargetWithStatus, "", 1, Vulnerability), effectYaml({ operation: TokenManipulation, hasToken: 1, token: Mark, stacks: 1, chance: 0.10, target: Opposite }));
  addPassive(assets, "buck_tree3_tier3_passive1", "Attackers 25% receive Mark", 1, 3, 3, 1, condYaml(UponDamageTaken), effectYaml({ operation: TokenManipulation, hasToken: 1, token: Mark, stacks: 2, chance: 0.25, target: Opposite }));
  addPassive(assets, "buck_tree3_tier3_passive2", "Pistol +50% damage per defeated enemy", 1, 3, 3, 2, skillIdConditions(Permanent, PISTOL), effectYaml({ operation: ExtraStatsFromResource, magnitude: 0.50, resource: EnemiesDefeatedThisBattle, characterStat: DamageCaused }));
  addPassive(assets, "buck_tree3_tier3_passive3", "Marks you apply +100% effective", 1, 3, 3, 3, condYaml(Permanent), effectYaml({ operation: TokenStatChange, magnitude: 1.0, hasToken: 1, token: Mark, target: Opposite }));
  return assets;
}

function tokenOnHit(token, stacks) {
  return { type: "ApplyToken", token, stacks, chance: 1 };
}

function skill(id, name, extras) {
  return { id, name, type: "Active", ...extras };
}

const SKILL_DEFS = {
  buckBasicHit: skill("buck_innate_active1", "revolver shot", { corruptionCost: 0, element: "Fire", targetKind: "OneEnemy", baseDamage: { min: 8, max: 13 }, baseCritChance: 0.05, accuracy: 0.55, effectsOnHit: [] }),
  buckPistol: skill("buck_innate_active2", "Pistol Draw", { corruptionCost: 2, element: "Fire", targetKind: "UpToThreeEnemies", baseDamage: { min: 4, max: 8 }, baseCritChance: 0.07, accuracy: 0.5, chanceToNotEndTurn: 0.75, effectsOnHit: [] }),
  buckRevolver: skill("buck_innate_active3", "Unload ammo", { corruptionCost: 3, element: "Fire", targetKind: "OneEnemy", baseDamage: { min: 5, max: 10 }, baseCritChance: 0.06, accuracy: 0.4, hitCount: 3, effectsOnHit: [] }),
  buckRifle: skill("buck_innate_active4", "Hunting Rifle", { corruptionCost: 4, element: "Anomaly", targetKind: "OneEnemy", baseDamage: { min: 15, max: 25 }, baseCritChance: 0.05, accuracy: 0.8, effectsOnHit: [tokenOnHit("Vulnerability", 3)] }),
  buckSpiderHands: skill("buck_tree1_tier1_active", "More hands to aim with", { corruptionCost: 4, element: "Anomaly", targetKind: "Self", baseDamage: { min: 0, max: 0 }, baseCritChance: 0, accuracy: 1, effectsOnHit: [tokenOnHit("Strength", 3), tokenOnHit("Dexterity", 3)] }),
  buckAllGuns: skill("buck_tree1_tier2_active", "Guns for all", { corruptionCost: 6, element: "Anomaly", targetKind: "OneEnemy", baseDamage: { min: 0, max: 0 }, baseCritChance: 0, accuracy: 0, followUpSkillIds: ["buck_innate_active2", "buck_innate_active3", "buck_innate_active4"], effectsOnHit: [] }),
  buckJuggle: skill("buck_tree1_tier3_active", "Juggling", { corruptionCost: 5, element: "Anomaly", targetKind: "Self", baseDamage: { min: 0, max: 0 }, baseCritChance: 0, accuracy: 1, accuracyPenaltyPerLivingEnemy: 0.1, grantsBonusActionsToAllies: true, effectsOnHit: [] }),
  buckSnakeVision: skill("buck_tree2_tier1_active", "Heat Vision", { corruptionCost: 4, element: "Anomaly", targetKind: "OneEnemy", baseDamage: { min: 0, max: 0 }, baseCritChance: 0, accuracy: 2, effectsOnHit: [tokenOnHit("Vulnerability", 3), tokenOnHit("Exposition", 3)] }),
  buckSnakeBite: skill("buck_tree2_tier2_active", "Corrosive bite", { corruptionCost: 3, element: "Anomaly", targetKind: "OneEnemy", baseDamage: { min: 3, max: 8 }, baseCritChance: 0.03, accuracy: 0.75, effectsOnHit: [tokenOnHit("Corrosion", 3)] }),
  buckSnakeTail: skill("buck_tree2_tier3_active", "Strangle", { corruptionCost: 5, element: "Anomaly", targetKind: "OneEnemy", baseDamage: { min: 0, max: 0 }, baseCritChance: 0, accuracy: 0, computeFromDebuffTypesOnTarget: true, damagePerDistinctDebuffType: 10, critChancePerDistinctDebuffType: 0.04, accuracyPerDistinctDebuffType: 0.3, effectsOnHit: [] }),
  buckMark: skill("buck_tree3_tier1_active", "Challenge for Duel", { corruptionCost: 3, element: "Fire", targetKind: "OneEnemy", baseDamage: { min: 0, max: 0 }, baseCritChance: 0, accuracy: 0.8, effectsOnHit: [tokenOnHit("Mark", 3)] }),
  buckPistolHeadShot: skill("buck_tree3_tier2_active", "Aim for the head", { corruptionCost: 5, element: "Fire", targetKind: "OneEnemy", baseDamage: { min: 4, max: 8 }, baseCritChance: 0.25, accuracy: 0.4, hitCount: 3, chanceToNotEndTurn: 0.9, effectsOnHit: [] }),
  buckLuckManipulation: skill("buck_tree3_tier3_active", "Lucky ammunition", { corruptionCost: 6, element: "Anomaly", targetKind: "Self", baseDamage: { min: 0, max: 0 }, baseCritChance: 0, accuracy: 1, effectsOnHit: [tokenOnHit("LuckyShot", 6)] }),
};

function updateJsonCatalog() {
  const skillsPath = join(STREAMING, "skills.json");
  const skills = JSON.parse(readFileSync(skillsPath, "utf8"));
  const rebuilt = skills.filter((entry) => !OLD_SKILL_ORDER.includes(entry.id));
  const insertAt = rebuilt.findIndex((entry) => entry.id.startsWith("maria"));
  rebuilt.splice(insertAt, 0, ...OLD_SKILL_ORDER.map((oldId) => SKILL_DEFS[oldId]));
  writeFileSync(skillsPath, JSON.stringify(rebuilt, null, 2) + "\n");

  const passivesPath = join(STREAMING, "passives.json");
  const passives = JSON.parse(readFileSync(passivesPath, "utf8"));
  const filtered = passives.filter((passive) => !passive.id.startsWith("b_ar_") && !passive.id.startsWith("b_sn_") && !passive.id.startsWith("b_du_"));
  filtered.push(...buildPassivesJson());
  writeFileSync(passivesPath, JSON.stringify(filtered, null, 2) + "\n");

  const treesPath = join(STREAMING, "skill_trees.json");
  let treesText = readFileSync(treesPath, "utf8");
  for (const [oldId, [newId]] of Object.entries(TREE_NODE_MAP).sort((left, right) => right[0].length - left[0].length)) {
    treesText = treesText.replaceAll(`"${oldId}"`, `"${newId}"`);
  }
  writeFileSync(treesPath, treesText);
}

function updateSkillTreeNodes() {
  for (const [oldId, [newId, displayName]] of Object.entries(TREE_NODE_MAP)) {
    const assetPath = join(NODES, `${oldId}.asset`);
    const text = readFileSync(assetPath, "utf8").split(/\r?\n/).map((line) => {
      if (line.startsWith("  _nodeId:")) return `  _nodeId: ${newId}`;
      if (line.startsWith("  _displayName:")) return `  _displayName: ${displayName}`;
      return line;
    }).join("\n");
    writeFileSync(assetPath, text.endsWith("\n") ? text : text + "\n");
  }
}

mkdirSync(ABILITY_DIR, { recursive: true });
const guids = {};
const orderedIds = [];
for (const [abilityId, yamlText] of [...buildActives(), ...buildPassiveAssets()]) {
  const id = guid();
  guids[abilityId] = id;
  writeFileSync(join(ABILITY_DIR, `${abilityId}.asset`), yamlText);
  writeMeta(join(ABILITY_DIR, `${abilityId}.asset.meta`), id);
  orderedIds.push(abilityId);
}
const preferred = [
  "buck_innate_active1", "buck_innate_active2", "buck_innate_active3", "buck_innate_active4",
  "buck_leader_passive1", "buck_companion_passive1",
  "buck_corruption_tier1_passive1", "buck_corruption_tier2_passive1", "buck_corruption_tier3_passive1",
];
const rest = orderedIds.filter((id) => !preferred.includes(id)).sort();
const kitLines = [
  "%YAML 1.1", "%TAG !u! tag:unity3d.com,2011:", "--- !u!114 &11400000", "MonoBehaviour:",
  "  m_ObjectHideFlags: 0", "  m_CorrespondingSourceObject: {fileID: 0}", "  m_PrefabInstance: {fileID: 0}",
  "  m_PrefabAsset: {fileID: 0}", "  m_GameObject: {fileID: 0}", "  m_Enabled: 1", "  m_EditorHideFlags: 0",
  `  m_Script: {fileID: 11500000, guid: ${KIT_SCRIPT}, type: 3}`, "  m_Name: BuckCombatKit",
  "  m_EditorClassIdentifier: ", "  _characterId: buck", "  _abilities:",
  ...preferred.concat(rest).map((id) => `  - {fileID: 11400000, guid: ${guids[id]}, type: 2}`),
];
writeFileSync(join(ABILITY_DIR, "BuckCombatKit.asset"), kitLines.join("\n") + "\n");
writeMeta(join(ABILITY_DIR, "BuckCombatKit.asset.meta"), guid());
updateJsonCatalog();
updateSkillTreeNodes();
console.log(`Wrote ${orderedIds.length} abilities to ${ABILITY_DIR}`);
