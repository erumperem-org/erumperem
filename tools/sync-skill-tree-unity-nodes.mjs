/**
 * Regenerates SkillTreeNode assets from skill_trees.json and remaps SkillTreePanel
 * presenter references from the old f_/m_/a_ / b_f_ ID scheme to the current kit IDs.
 *
 * Usage: node tools/sync-skill-tree-unity-nodes.mjs
 */
import fs from "fs";
import path from "path";
import crypto from "crypto";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const projectRoot = path.resolve(__dirname, "..");
const nodesDir = path.join(projectRoot, "Assets/_Project/Resources/SkillTreeNodes");
const treesPath = path.join(projectRoot, "Game.Simulations/Data/skill_trees.json");
const skillsPath = path.join(projectRoot, "Game.Simulations/Data/skills.json");
const passivesPath = path.join(projectRoot, "Game.Simulations/Data/passives.json");
const panelPrefabPath = path.join(
  projectRoot,
  "Assets/_Project/Prefabs/UIPrefabs/SkillTreePanel.prefab"
);
const overworldScenePath = path.join(projectRoot, "Assets/__Scenes/Overworld.unity");
const scriptGuid = "b845edd710b095d42ada371b8ab1f8c3";

const ElementEnum = { None: 0, Fire: 1, Metal: 2, Anomaly: 3 };

const trees = JSON.parse(fs.readFileSync(treesPath, "utf8"));
const skills = JSON.parse(fs.readFileSync(skillsPath, "utf8"));
const passives = JSON.parse(fs.readFileSync(passivesPath, "utf8"));
const skillById = Object.fromEntries(skills.map((skill) => [skill.id, skill]));
const passiveById = Object.fromEntries(passives.map((passive) => [passive.id, passive]));

function yamlEscape(value) {
  if (value == null) return '""';
  const text = String(value);
  if (/[:#\[\]\{\},&\*?|<>=!%@`'\n"]/.test(text) || text !== text.trim()) {
    return JSON.stringify(text);
  }
  return text;
}

function createGuid() {
  return crypto.randomBytes(16).toString("hex");
}

function buildNodeListByCharacter() {
  const byCharacter = {};
  for (const character of trees) {
    const ordered = [];
    for (const tree of character.trees) {
      for (const tier of tree.tiers) {
        for (const node of tier.nodes) {
          ordered.push({
            id: node.id,
            type: node.type,
            element: tree.element,
            tier: tier.tier,
          });
        }
      }
    }
    byCharacter[character.characterId] = ordered;
  }
  return byCharacter;
}

function resolveOldIdToNewId(oldId, nodesByCharacter) {
  const buckMatch = oldId.match(/^b_([fma])_t(\d+)_(p\d+|a\d+)$/i);
  if (buckMatch) {
    const elementKey = buckMatch[1].toLowerCase();
    const tier = Number(buckMatch[2]);
    const slot = buckMatch[3].toLowerCase();
    const elementIndex = { f: 0, m: 1, a: 2 }[elementKey];
    const slotIndex = slot.startsWith("p") ? Number(slot.slice(1)) - 1 : 3;
    const tree = trees.find((entry) => entry.characterId === "buck");
    return tree.trees[elementIndex].tiers[tier - 1].nodes[slotIndex].id;
  }

  const wulfricMatch = oldId.match(/^([fma])_t(\d+)_(p\d+|a\d+)$/i);
  if (wulfricMatch) {
    const elementKey = wulfricMatch[1].toLowerCase();
    const tier = Number(wulfricMatch[2]);
    const slot = wulfricMatch[3].toLowerCase();
    const elementIndex = { f: 0, m: 1, a: 2 }[elementKey];
    const slotIndex = slot.startsWith("p") ? Number(slot.slice(1)) - 1 : 3;
    const tree = trees.find((entry) => entry.characterId === "wulfric");
    return tree.trees[elementIndex].tiers[tier - 1].nodes[slotIndex].id;
  }

  // Already a current id
  for (const ordered of Object.values(nodesByCharacter)) {
    if (ordered.some((node) => node.id === oldId)) {
      return oldId;
    }
  }

  return null;
}

function writeNodeAsset(nodeMeta, guid) {
  const isPassive = String(nodeMeta.type).toLowerCase() === "passive";
  const passive = passiveById[nodeMeta.id];
  const skill = skillById[nodeMeta.id];
  const displayName = isPassive
    ? passive?.id ?? nodeMeta.id
    : skill?.name ?? nodeMeta.id;
  const description = isPassive
    ? `Passive ${nodeMeta.id}`
    : skill
      ? `${skill.type ?? "Active"} (${skill.element}) — damage ${skill.baseDamage?.min ?? 0}-${skill.baseDamage?.max ?? 0}.`
      : `Active ${nodeMeta.id}`;
  const elementValue = ElementEnum[nodeMeta.element] ?? 0;

  const assetBody = `%YAML 1.1
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
  m_Name: ${nodeMeta.id}
  m_EditorClassIdentifier: Assembly-CSharp::Erumperem.Progression.SkillTreeNodeAsset
  _nodeId: ${nodeMeta.id}
  _skillTreeElementCategory: ${elementValue}
  _isPassiveNode: ${isPassive ? 1 : 0}
  _displayName: ${yamlEscape(displayName)}
  _descriptionForUi: ${yamlEscape(description)}
  _activeSkillTypeLabel: Active
  _activeSkillDamageElement: ${elementValue}
  _baseDamageMinimum: ${skill?.baseDamage?.min ?? 0}
  _baseDamageMaximum: ${skill?.baseDamage?.max ?? 0}
  _baseCriticalHitChanceFraction: ${skill?.baseCritChance ?? 0}
  _baseHitAccuracyFraction: ${skill?.accuracy ?? 1}
  _targetSelectionKind: 0
  _aiAbsoluteChanceToConsiderWhenEligible: 1
  _aiOnlyEligibleWhenOwnHpFractionBelow: 1
  _corruptionCostAddedWhenPlayerCasts: ${skill?.corruptionCost ?? 1}
  _effectsAppliedAfterSuccessfulHit: []
  _extraEffectsWhenTargetHasComboToken: []
  _passiveEffectKind: 0
  _passiveAppliesWhenSkillIdMatches: ${yamlEscape(passive?.skillId ?? "")}
  _passivePrerequisiteSkillIdThatMustBeUsedFirst: 
  _passiveUsesDotTypeFilter: 0
  _passiveDotTypeFilter: 0
  _passiveUsesTokenTypeFilter: 0
  _passiveTokenTypeFilter: 0
  _passiveGrantsExtraTokenOfType: 0
  _passiveTokenTypeToGrantWhenTriggered: 0
  _passiveOnlyAppliesWhenActorHasTokenType: 0
  _passiveRequiredTokenTypeOnActor: 0
  _passiveOnlyAppliesWhenActorLacksTokenType: 0
  _passiveBlockingTokenTypeOnActor: 0
  _passiveDamageBonusOrIncomingMultiplierMagnitude: ${passive?.additive ?? 0}
  _passiveDamageBonusFractionPerDotStackOnTarget: 0
  _passiveDamageBonusFractionMaximumCap: 0
  _passiveActivatesWhenHpFractionBelow: 0
  _passiveStacksOrDotPotencyOrTurnsBonusInteger: 0
  _passiveDotDurationOrMaxTurnCapInteger: 0
  _unityEventFiresOnlyForThesePassiveTriggers: 
  _unityEventInvokedWhenPassiveTriggerFires:
    m_PersistentCalls:
      m_Calls: []
`;

  const metaBody = `fileFormatVersion: 2
guid: ${guid}
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData: 
  assetBundleName: 
  assetBundleVariant: 
`;

  fs.writeFileSync(path.join(nodesDir, `${nodeMeta.id}.asset`), assetBody);
  fs.writeFileSync(path.join(nodesDir, `${nodeMeta.id}.asset.meta`), metaBody);
}

function loadExistingGuidMap() {
  const guidToId = {};
  const idToGuid = {};
  for (const fileName of fs.readdirSync(nodesDir)) {
    if (!fileName.endsWith(".asset.meta")) continue;
    const nodeId = fileName.replace(".asset.meta", "");
    const meta = fs.readFileSync(path.join(nodesDir, fileName), "utf8");
    const match = meta.match(/guid: ([a-f0-9]+)/);
    if (!match) continue;
    guidToId[match[1]] = nodeId;
    idToGuid[nodeId] = match[1];
  }
  return { guidToId, idToGuid };
}

function main() {
  fs.mkdirSync(nodesDir, { recursive: true });
  const nodesByCharacter = buildNodeListByCharacter();
  const allNodes = Object.values(nodesByCharacter).flat();

  const { guidToId: existingGuidToId, idToGuid } = loadExistingGuidMap();

  let createdCount = 0;
  for (const nodeMeta of allNodes) {
    if (!idToGuid[nodeMeta.id]) {
      idToGuid[nodeMeta.id] = createGuid();
      createdCount++;
    }
    writeNodeAsset(nodeMeta, idToGuid[nodeMeta.id]);
  }

  const oldGuidToNewGuid = {};
  for (const [oldGuid, oldId] of Object.entries(existingGuidToId)) {
    const newId = resolveOldIdToNewId(oldId, nodesByCharacter);
    if (!newId || !idToGuid[newId]) continue;
    if (oldGuid !== idToGuid[newId]) {
      oldGuidToNewGuid[oldGuid] = idToGuid[newId];
    }
  }

  let remappedReferences = 0;
  let panelText = fs.readFileSync(panelPrefabPath, "utf8");
  panelText = panelText.replace(
    /guid: ([a-f0-9]{32})/g,
    (full, guid) => {
      const replacement = oldGuidToNewGuid[guid];
      if (!replacement) return full;
      remappedReferences++;
      return `guid: ${replacement}`;
    }
  );
  fs.writeFileSync(panelPrefabPath, panelText);

  let overworldText = fs.readFileSync(overworldScenePath, "utf8");
  const beforeLevel = overworldText;
  overworldText = overworldText.replace(
    /(_initialSharedSkillLevel:\s*)0\b/,
    "$112"
  );
  overworldText = overworldText.replace(
    /guid: ([a-f0-9]{32})/g,
    (full, guid) => {
      const replacement = oldGuidToNewGuid[guid];
      if (!replacement) return full;
      remappedReferences++;
      return `guid: ${replacement}`;
    }
  );
  fs.writeFileSync(overworldScenePath, overworldText);

  console.log(
    JSON.stringify(
      {
        nodesWritten: allNodes.length,
        newlyCreatedGuids: createdCount,
        oldToNewGuidMappings: Object.keys(oldGuidToNewGuid).length,
        remappedGuidOccurrences: remappedReferences,
        overworldInitialLevelUpdated: beforeLevel !== overworldText,
        sampleMappings: Object.entries(existingGuidToId)
          .slice(0, 8)
          .map(([guid, oldId]) => ({
            oldId,
            newId: resolveOldIdToNewId(oldId, nodesByCharacter),
            newGuid: oldGuidToNewGuid[guid],
          })),
      },
      null,
      2
    )
  );
}

main();
