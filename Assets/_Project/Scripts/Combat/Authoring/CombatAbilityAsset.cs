using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;
using UnityEngine;

namespace Erumperem.Combat.Authoring
{
    /// <summary>
    /// Designer authoring surface for one combat ability (active skill or passive).
    /// Duplicate this asset, fill the Inspector, then run Erumperem/Combat/Export Catalog.
    /// Runtime combat never reads this ScriptableObject — only the exported StreamingAssets JSON.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatAbility", menuName = "Erumperem/Combat/Ability")]
    public sealed class CombatAbilityAsset : ScriptableObject
    {
        [Serializable]
        public sealed class SerializableEffectSpec
        {
            [Tooltip("Combat effect applied after a successful hit (token, DoT, heal, push, etc.).")]
            public EffectType Type;

            [Range(0f, 1f)]
            [Tooltip("Absolute chance (0..1) that this effect applies after the hit connects.")]
            public double Chance = 1.0;

            [Tooltip("Token stacks granted or consumed, depending on Type.")]
            public int Stacks;

            [Tooltip("Heal amount, DoT potency, or self-damage amount depending on Type.")]
            public int Potency;

            [Tooltip("When greater than Potency on HealHp, heal rolls in [Potency, AmountMax].")]
            public int AmountMax;

            [Tooltip("Duration in turns for DoTs or timed effects.")]
            public int Duration;

            [Tooltip("Self-damage steps per consumed token stack (Loss of control).")]
            public int Steps;

            [Tooltip("Who receives this effect relative to the hit: Default = hit target, Self = caster, AllAllies / AllEnemies.")]
            public EffectScope EffectScope = EffectScope.Default;

            [Tooltip("ON writes Token onto the exported EffectSpec (ApplyToken / consume-token effects).")]
            public bool HasToken;

            public TokenType Token;

            [Tooltip("ON writes Dot onto the exported EffectSpec (ApplyDot).")]
            public bool HasDot;

            public DotType Dot;

            [Tooltip("ON adds extra stacks from the caster's token count (Whip Sword, Protect The Weak).")]
            public bool HasScaleFromToken;

            public TokenType ScaleFromToken;

            [Tooltip("Extra stacks = (caster stacks of ScaleFromToken * this) / divisor.")]
            public int ScaleStacksPerSourceStack;

            [Tooltip("Divisor for ScaleFromToken (e.g. +1 Defense per 2 ControlledInstability → 2). Minimum 1 when scaling.")]
            public int ScaleStacksSourceDivisor = 1;

            public EffectSpec ToRuntimeSpec()
            {
                return new EffectSpec
                {
                    Type = Type,
                    Chance = Chance,
                    Stacks = Stacks,
                    Potency = Potency,
                    AmountMax = AmountMax,
                    Duration = Duration,
                    Steps = Steps,
                    EffectScope = EffectScope,
                    Token = HasToken ? Token : null,
                    Dot = HasDot ? Dot : null,
                    ScaleFromToken = HasScaleFromToken ? ScaleFromToken : null,
                    ScaleStacksPerSourceStack = ScaleStacksPerSourceStack,
                    ScaleStacksSourceDivisor = ScaleStacksSourceDivisor <= 0 ? 1 : ScaleStacksSourceDivisor,
                };
            }
        }

        [Serializable]
        public sealed class SerializablePassiveCondition
        {
            [Tooltip("When this passive is allowed to consider firing. Engine evaluation is a later phase.")]
            public PassiveActivationKind Activation = PassiveActivationKind.Permanent;

            [Tooltip("ON requires RequiredStatus on the relevant combatant.")]
            public bool HasRequiredStatus;

            public TokenType RequiredStatus;

            public PassiveStatusMatchKind StatusMatch;

            [Tooltip("UponDamageTaken: fire once per this many HP lost. 0 = unused.")]
            public int HitPointsLostPerTrigger;

            [Tooltip("UponStatThreshold: Current/Max fraction. 0 = unused.")]
            public double StatThresholdFraction;

            public PassiveStatThresholdComparison StatThresholdComparison;

            [Tooltip("Optional skill id this condition watches.")]
            public string SkillId = "";

            public PassiveConditionDefinition ToRuntimeCondition()
            {
                return new PassiveConditionDefinition
                {
                    Activation = Activation,
                    RequiredStatus = HasRequiredStatus ? RequiredStatus : null,
                    StatusMatch = StatusMatch,
                    HitPointsLostPerTrigger = HitPointsLostPerTrigger,
                    StatThresholdFraction = StatThresholdFraction,
                    StatThresholdComparison = StatThresholdComparison,
                    SkillId = string.IsNullOrWhiteSpace(SkillId) ? null : SkillId.Trim(),
                };
            }
        }

        [Serializable]
        public sealed class SerializablePassiveEffect
        {
            [Tooltip("What the passive does when it fires. Engine application is a later phase.")]
            public PassiveEffectOperationKind Operation;

            [Tooltip("Numeric magnitude (percent points, damage, heal, extra stats).")]
            public double Magnitude;

            [Tooltip("ON writes Token for TokenStatChange / TokenManipulation.")]
            public bool HasToken;

            public TokenType Token;

            [Tooltip("Skill id for SkillStatChange or CastSkill.")]
            public string SkillId = "";

            [Tooltip("Enemy id for SummonEnemy (enemy passives only, e.g. Horse Boss).")]
            public string SummonEnemyId = "";

            [Tooltip("Stacks granted or consumed.")]
            public int Stacks;

            [Tooltip("Chance for this effect when the passive fires. 1 = always.")]
            public double ChanceToTrigger = 1.0;

            [Tooltip("0 = unlimited for the battle.")]
            public int MaxTriggersPerBattle;

            [Tooltip("0 = unlimited for the turn.")]
            public int MaxTriggersPerTurn;

            public PassiveCharacterStatKind CharacterStat;

            public PassiveSkillStatKind SkillStat;

            public PassiveResourceKind Resource;

            public PassiveStatChangeTarget StatChangeTarget;

            public PassiveTokenManipulationMode TokenManipulationMode;

            [Tooltip("ON writes ResourceToken when Token is the output (Destab efficiency per CI).")]
            public bool HasResourceToken;

            public TokenType ResourceToken;

            [Tooltip("Extra token stacks from the event delta. 0 = unused.")]
            public int ScaleStacksPerSourceStack;

            [Tooltip("Divisor for ScaleStacksPerSourceStack. 0 = unused.")]
            public int ScaleStacksSourceDivisor;

            [Tooltip("CharacterStatChange lasts until this combatant's next turn start.")]
            public bool ExpiresAtEndOfOpposingSideTurn;

            [Tooltip("Absolute chance that one stack of Token is not lost at end of turn.")]
            public double SkipEndOfTurnDecayChance;

            public PassiveEffectDefinition ToRuntimeEffect()
            {
                return new PassiveEffectDefinition
                {
                    Operation = Operation,
                    Magnitude = Magnitude,
                    Token = HasToken ? Token : null,
                    SkillId = string.IsNullOrWhiteSpace(SkillId) ? null : SkillId.Trim(),
                    SummonEnemyId = string.IsNullOrWhiteSpace(SummonEnemyId) ? null : SummonEnemyId.Trim(),
                    Stacks = Stacks,
                    ChanceToTrigger = ChanceToTrigger <= 0 ? 1.0 : ChanceToTrigger,
                    MaxTriggersPerBattle = MaxTriggersPerBattle,
                    MaxTriggersPerTurn = MaxTriggersPerTurn,
                    CharacterStat = CharacterStat,
                    SkillStat = SkillStat,
                    Resource = Resource,
                    StatChangeTarget = StatChangeTarget,
                    TokenManipulationMode = TokenManipulationMode,
                    ResourceToken = HasResourceToken ? ResourceToken : null,
                    ScaleStacksPerSourceStack = ScaleStacksPerSourceStack,
                    ScaleStacksSourceDivisor = ScaleStacksSourceDivisor,
                    ExpiresAtEndOfOpposingSideTurn = ExpiresAtEndOfOpposingSideTurn,
                    SkipEndOfTurnDecayChance = SkipEndOfTurnDecayChance,
                };
            }
        }

        [Header("Identity")]
        [Tooltip("Canonical snake_case id written to JSON (e.g. wulfric_innate_active1). Must be unique in the catalog.")]
        [SerializeField] private string _abilityId = "";

        [Tooltip("Player-facing name exported as SkillDefinition.Name (actives) or left as the passive id when empty.")]
        [SerializeField] private string _displayName = "";

        [Tooltip("Owner character id: wulfric, buck, maria, or an enemy id for EnemyPassive. Not Matsuda.")]
        [SerializeField] private string _ownerCharacterId = "";

        [Tooltip("Active = skill in skills.json. Passive = entry in passives.json.")]
        [SerializeField] private CombatAbilityKind _abilityKind = CombatAbilityKind.Active;

        [Tooltip("InnateActive / TreeNode / LeaderPassive / CompanionPassive / CorruptionPassive / EnemyPassive.")]
        [SerializeField] private CombatAbilityPlacement _placement = CombatAbilityPlacement.InnateActive;

        [Tooltip("Tree column 1–3 for TreeNode placement (1 = first tree of this character). Ignored otherwise.")]
        [SerializeField] private int _treeIndex = 1;

        [Tooltip("Tier 1–3 inside that tree for TreeNode placement. Ignored otherwise.")]
        [SerializeField] private int _tierIndex = 1;

        [Tooltip("Passive slot 1–3 inside the tier for TreeNode passives. Ignored for actives.")]
        [SerializeField] private int _passiveIndex = 1;

        [Tooltip("CorruptionPassive: minimum corruption tier (1–3) required. Ignored otherwise.")]
        [SerializeField] private int _corruptionMinTier;

        [Tooltip("Designer notes only. Not exported to the runtime catalog.")]
        [TextArea(2, 8)]
        [SerializeField] private string _designerNotes = "";

        [Header("Active skill — core")]
        [Tooltip("Free-form SkillDefinition.Type label. Defaults to Active.")]
        [SerializeField] private string _activeSkillTypeLabel = "Active";

        [Tooltip("Damage element. None falls back to the actor affinity at resolve time.")]
        [SerializeField] private ElementType _activeSkillDamageElement;

        [Tooltip("Inclusive minimum of the random base damage roll.")]
        [SerializeField] private int _baseDamageMinimum;

        [Tooltip("Inclusive maximum of the random base damage roll.")]
        [SerializeField] private int _baseDamageMaximum;

        [Tooltip("Critical chance on a successful hit before stat modifiers. May exceed 1.0 if the kit asks for it.")]
        [SerializeField] private double _baseCriticalHitChanceFraction;

        [Tooltip("Hit chance before the actor accuracy stat. Values above 1.0 are allowed (e.g. 2.0).")]
        [SerializeField] private double _baseHitAccuracyFraction = 1.0;

        [Tooltip("Who is selected and who takes primary damage.")]
        [SerializeField] private SkillTargetKind _targetSelectionKind = SkillTargetKind.OneEnemy;

        [Range(0f, 1f)]
        [Tooltip("AI: absolute chance to consider this skill when eligible. 1 = always.")]
        [SerializeField] private double _aiAbsoluteChanceToConsiderWhenEligible = 1.0;

        [Range(0f, 1f)]
        [Tooltip("AI: only eligible while CurrentHp/MaxHp is below this fraction. 1 = no HP gate.")]
        [SerializeField] private double _aiOnlyEligibleWhenOwnHpFractionBelow = 1.0;

        [Tooltip("World corruption added when a player casts this skill. Enemies do not pay this cost.")]
        [SerializeField] private double _corruptionCostAddedWhenPlayerCasts = CorruptionRules.DefaultSkillCorruptionCost;

        [Header("Active skill — extra combat fields")]
        [Tooltip("Independent hit/damage rolls against each primary target (Unload=3, Frenzy=5). Minimum 1.")]
        [SerializeField] private int _hitCount = 1;

        [Range(0f, 1f)]
        [Tooltip("Chance the actor keeps their turn after a successful cast (grants BonusAction).")]
        [SerializeField] private double _chanceToNotEndTurn;

        [Tooltip("Skills resolved immediately after this one against the same selected target (Guns for all).")]
        [SerializeField] private List<string> _followUpSkillIds = new();

        [Tooltip("After a successful cast, grant BonusAction to self and living allies (Juggling).")]
        [SerializeField] private bool _grantsBonusActionsToAllies;

        [Tooltip("Subtracted from accuracy once per living enemy on the opposite side (Juggling).")]
        [SerializeField] private double _accuracyPenaltyPerLivingEnemy;

        [Tooltip("ON adds flat bonus damage per stack of BonusDamagePerOwnToken on the caster.")]
        [SerializeField] private bool _hasBonusDamagePerOwnToken;

        [Tooltip("Token on the caster that adds flat damage (Shield Charge + Defense).")]
        [SerializeField] private TokenType _bonusDamagePerOwnToken;

        [Tooltip("Flat damage added per stack of that token. Minimum 1 when the token is set.")]
        [SerializeField] private int _bonusDamagePerOwnTokenStacks = 1;

        [Tooltip("Strangle-style: damage/crit/accuracy scale with distinct debuff types on the target.")]
        [SerializeField] private bool _computeFromDebuffTypesOnTarget;

        [Tooltip("Flat damage added per distinct debuff type on the target.")]
        [SerializeField] private int _damagePerDistinctDebuffType;

        [Tooltip("Crit chance added per distinct debuff type on the target.")]
        [SerializeField] private double _critChancePerDistinctDebuffType;

        [Tooltip("Accuracy added per distinct debuff type on the target.")]
        [SerializeField] private double _accuracyPerDistinctDebuffType;

        [Tooltip("Allow SelfAndAlly / OneAlly pools to include dead allies (Resurrection Hymn).")]
        [SerializeField] private bool _canTargetDeadAllies;

        [Header("Active skill — effects on hit")]
        [Tooltip("Effects applied after a successful hit. AmountMax, ScaleFromToken, EffectScope must be filled here — export does not drop them.")]
        [SerializeField] private List<SerializableEffectSpec> _effectsAppliedAfterSuccessfulHit = new();

        [Header("Passive — role and trigger caps")]
        [Tooltip("Any = no role gate. Leader / Companion skip the passive unless the combatant's PartyRole matches.")]
        [SerializeField] private PassiveRequiredPartyRole _requiredPartyRole = PassiveRequiredPartyRole.Any;

        [Range(0f, 1f)]
        [Tooltip("Chance to trigger when conditions match. Default 1.")]
        [SerializeField] private double _chanceToTrigger = 1.0;

        [Tooltip("0 = unlimited triggers per battle.")]
        [SerializeField] private int _maxTriggersPerBattle;

        [Tooltip("0 = unlimited triggers per turn.")]
        [SerializeField] private int _maxTriggersPerTurn;

        [Header("Passive — conditions and effects (new model, data only)")]
        [Tooltip("Conditions that must match. Serialized to passives.json; not evaluated by the engine yet.")]
        [SerializeField] private List<SerializablePassiveCondition> _passiveConditions = new();

        [Tooltip("Effects applied when the passive fires. Serialized to passives.json; not applied by the engine yet.")]
        [SerializeField] private List<SerializablePassiveEffect> _passiveEffects = new();

        [Header("Passive — legacy EffectKind (export compatibility)")]
        [Tooltip("Existing PassiveDefinition.EffectKind so current JSON/runtime still load. Do not add new enum cases; prefer Conditions + Effects.")]
        [SerializeField] private PassiveEffectKind _legacyPassiveEffectKind;

        [SerializeField] private string _legacyPassiveSkillId = "";
        [SerializeField] private string _legacyPassivePrerequisiteSkillId = "";
        [SerializeField] private bool _legacyPassiveUsesDotTypeFilter;
        [SerializeField] private DotType _legacyPassiveDotTypeFilter;
        [SerializeField] private bool _legacyPassiveUsesTokenTypeFilter;
        [SerializeField] private TokenType _legacyPassiveTokenTypeFilter;
        [SerializeField] private bool _legacyPassiveGrantsExtraTokenOfType;
        [SerializeField] private TokenType _legacyPassiveTokenTypeToGrantWhenTriggered;
        [SerializeField] private bool _legacyPassiveOnlyAppliesWhenActorHasTokenType;
        [SerializeField] private TokenType _legacyPassiveRequiredTokenTypeOnActor;
        [SerializeField] private bool _legacyPassiveOnlyAppliesWhenActorLacksTokenType;
        [SerializeField] private TokenType _legacyPassiveBlockingTokenTypeOnActor;
        [SerializeField] private double _legacyPassiveAdditive;
        [SerializeField] private double _legacyPassiveAdditivePerStack;
        [SerializeField] private double _legacyPassiveCap;
        [SerializeField] private double _legacyPassiveHpBelowPercent;
        [SerializeField] private int _legacyPassiveIntValue;
        [SerializeField] private int _legacyPassiveIntValue2;

        [Header("Tree node (TreeNode placement only)")]
        [Tooltip("Unlock cost written to skill_trees.json. Default 1.")]
        [SerializeField] private int _treeNodeUnlockCost = 1;

        [Tooltip("Node ids that must be unlocked first (usually the three passives of the same tier for an active).")]
        [SerializeField] private List<string> _treeNodePrerequisiteAbilityIds = new();

        public string AbilityId => _abilityId?.Trim() ?? "";
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? AbilityId : _displayName.Trim();
        public string OwnerCharacterId => _ownerCharacterId?.Trim() ?? "";
        public CombatAbilityKind AbilityKind => _abilityKind;
        public CombatAbilityPlacement Placement => _placement;
        public int TreeIndex => _treeIndex;
        public int TierIndex => _tierIndex;
        public int PassiveIndex => _passiveIndex;
        public int CorruptionMinTier => _corruptionMinTier;
        public int TreeNodeUnlockCost => _treeNodeUnlockCost;
        public IReadOnlyList<string> TreeNodePrerequisiteAbilityIds => _treeNodePrerequisiteAbilityIds ?? new List<string>();

        public bool IsActiveAbility => _abilityKind == CombatAbilityKind.Active;
        public bool IsPassiveAbility => _abilityKind == CombatAbilityKind.Passive;

        public SkillDefinition ToRuntimeSkillDefinition()
        {
            if (!IsActiveAbility || string.IsNullOrWhiteSpace(AbilityId))
            {
                throw new InvalidOperationException(
                    $"{name}: ToRuntimeSkillDefinition requires AbilityKind=Active and a non-empty abilityId.");
            }

            var clampedDamageMaximum = Math.Max(_baseDamageMinimum, _baseDamageMaximum);
            var clampedDamageMinimum = Math.Min(_baseDamageMinimum, _baseDamageMaximum);
            var hitCount = Math.Max(1, _hitCount);
            var bonusDamagePerOwnTokenStacks = Math.Max(1, _bonusDamagePerOwnTokenStacks);

            return new SkillDefinition
            {
                Id = AbilityId,
                Name = DisplayName,
                Element = _activeSkillDamageElement,
                Type = string.IsNullOrWhiteSpace(_activeSkillTypeLabel) ? "Active" : _activeSkillTypeLabel.Trim(),
                BaseDamage = new DamageRange { Min = clampedDamageMinimum, Max = clampedDamageMaximum },
                BaseCritChance = _baseCriticalHitChanceFraction,
                Accuracy = _baseHitAccuracyFraction,
                TargetKind = _targetSelectionKind,
                EffectsOnHit = (_effectsAppliedAfterSuccessfulHit ?? Enumerable.Empty<SerializableEffectSpec>())
                    .Select(effectSpec => effectSpec.ToRuntimeSpec())
                    .ToList(),
                ChanceToUse = _aiAbsoluteChanceToConsiderWhenEligible,
                SelfHpPercentBelow = _aiOnlyEligibleWhenOwnHpFractionBelow,
                CorruptionCost = _corruptionCostAddedWhenPlayerCasts,
                HitCount = hitCount,
                ChanceToNotEndTurn = _chanceToNotEndTurn,
                FollowUpSkillIds = SanitizeIdList(_followUpSkillIds),
                GrantsBonusActionsToAllies = _grantsBonusActionsToAllies,
                AccuracyPenaltyPerLivingEnemy = _accuracyPenaltyPerLivingEnemy,
                BonusDamagePerOwnToken = _hasBonusDamagePerOwnToken ? _bonusDamagePerOwnToken : null,
                BonusDamagePerOwnTokenStacks = bonusDamagePerOwnTokenStacks,
                ComputeFromDebuffTypesOnTarget = _computeFromDebuffTypesOnTarget,
                DamagePerDistinctDebuffType = _damagePerDistinctDebuffType,
                CritChancePerDistinctDebuffType = _critChancePerDistinctDebuffType,
                AccuracyPerDistinctDebuffType = _accuracyPerDistinctDebuffType,
                CanTargetDeadAllies = _canTargetDeadAllies,
            };
        }

        public PassiveDefinition ToRuntimePassiveDefinition()
        {
            if (!IsPassiveAbility || string.IsNullOrWhiteSpace(AbilityId))
            {
                throw new InvalidOperationException(
                    $"{name}: ToRuntimePassiveDefinition requires AbilityKind=Passive and a non-empty abilityId.");
            }

            return new PassiveDefinition
            {
                Id = AbilityId,
                EffectKind = _legacyPassiveEffectKind,
                SkillId = EmptyToNull(_legacyPassiveSkillId),
                PrerequisiteSkillId = EmptyToNull(_legacyPassivePrerequisiteSkillId),
                DotType = _legacyPassiveUsesDotTypeFilter ? _legacyPassiveDotTypeFilter : null,
                TokenType = _legacyPassiveUsesTokenTypeFilter ? _legacyPassiveTokenTypeFilter : null,
                GrantTokenType = _legacyPassiveGrantsExtraTokenOfType ? _legacyPassiveTokenTypeToGrantWhenTriggered : null,
                IfHasTokenType = _legacyPassiveOnlyAppliesWhenActorHasTokenType ? _legacyPassiveRequiredTokenTypeOnActor : null,
                UnlessHasTokenType = _legacyPassiveOnlyAppliesWhenActorLacksTokenType ? _legacyPassiveBlockingTokenTypeOnActor : null,
                Additive = _legacyPassiveAdditive,
                AdditivePerStack = _legacyPassiveAdditivePerStack,
                Cap = _legacyPassiveCap,
                HpBelowPercent = _legacyPassiveHpBelowPercent,
                IntValue = _legacyPassiveIntValue,
                IntValue2 = _legacyPassiveIntValue2,
                RequiredPartyRole = _requiredPartyRole,
                ChanceToTrigger = _chanceToTrigger,
                MaxTriggersPerBattle = _maxTriggersPerBattle,
                MaxTriggersPerTurn = _maxTriggersPerTurn,
                CorruptionMinTier = _corruptionMinTier,
                Conditions = (_passiveConditions ?? Enumerable.Empty<SerializablePassiveCondition>())
                    .Select(condition => condition.ToRuntimeCondition())
                    .ToList(),
                Effects = (_passiveEffects ?? Enumerable.Empty<SerializablePassiveEffect>())
                    .Select(effect => effect.ToRuntimeEffect())
                    .ToList(),
            };
        }

        public SkillTreeNodeDefinition ToRuntimeSkillTreeNodeDefinition()
        {
            if (_placement != CombatAbilityPlacement.TreeNode || string.IsNullOrWhiteSpace(AbilityId))
            {
                throw new InvalidOperationException(
                    $"{name}: ToRuntimeSkillTreeNodeDefinition requires Placement=TreeNode and a non-empty abilityId.");
            }

            return new SkillTreeNodeDefinition
            {
                Id = AbilityId,
                Type = IsPassiveAbility ? "Passive" : "Active",
                Cost = Math.Max(0, _treeNodeUnlockCost),
                Requires = SanitizeIdList(_treeNodePrerequisiteAbilityIds),
            };
        }

        private static IReadOnlyList<string> SanitizeIdList(List<string> rawIds)
        {
            if (rawIds == null || rawIds.Count == 0)
            {
                return Array.Empty<string>();
            }

            return rawIds
                .Where(skillId => !string.IsNullOrWhiteSpace(skillId))
                .Select(skillId => skillId.Trim())
                .ToList();
        }

        private static string EmptyToNull(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_baseDamageMaximum < _baseDamageMinimum)
            {
                (_baseDamageMinimum, _baseDamageMaximum) = (_baseDamageMaximum, _baseDamageMinimum);
            }

            _hitCount = Math.Max(1, _hitCount);
            _treeIndex = Math.Clamp(_treeIndex, 1, 3);
            _tierIndex = Math.Clamp(_tierIndex, 1, 3);
            _passiveIndex = Math.Clamp(_passiveIndex, 1, 3);
            _corruptionMinTier = Math.Clamp(_corruptionMinTier, 0, 3);
            _bonusDamagePerOwnTokenStacks = Math.Max(1, _bonusDamagePerOwnTokenStacks);
            _treeNodeUnlockCost = Math.Max(0, _treeNodeUnlockCost);

            if (_effectsAppliedAfterSuccessfulHit == null)
            {
                return;
            }

            foreach (var effectSpec in _effectsAppliedAfterSuccessfulHit)
            {
                if (effectSpec.ScaleStacksSourceDivisor <= 0)
                {
                    effectSpec.ScaleStacksSourceDivisor = 1;
                }
            }
        }
#endif
    }
}
