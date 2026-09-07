using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Erumperem.Progression
{
    /// <summary>
    /// Single ScriptableObject per skill-tree node (passive or active): tree identity, UI, full combat data,
    /// and optional Unity passive presentation hooks. Use as the only authoring surface for this node.
    /// Names are deliberately long so the Inspector and authors do not need extra documentation to
    /// understand each field; tooltips give the per-field semantic where it changes per effect kind.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillTreeNode", menuName = "Erumperem/Progression/Skill Tree Node (full)")]
    public sealed class SkillTreeNodeAsset : ScriptableObject
    {
        [Serializable]
        public sealed class SerializableEffectSpec
        {
            public EffectType Type;
            [Range(0, 1)] public double Chance = 1.0;
            public int Stacks;
            public int Potency;
            public int Duration;
            public int Steps;
            [FormerlySerializedAs("EffectScope")]
            public string EffectScopeName = "Default";
            public bool UseToken;
            public TokenType Token;
            public bool UseDot;
            public DotType Dot;

            public EffectSpec ToRuntimeSpec()
            {
                return new()
                {
                    Type = Type,
                    Chance = Chance,
                    Stacks = Stacks,
                    Potency = Potency,
                    Duration = Duration,
                    Steps = Steps,
                    EffectScope = ParseEffectScope(EffectScopeName),
                    Token = UseToken ? Token : null,
                    Dot = UseDot ? Dot : null,
                };
            }

            private static EffectScope ParseEffectScope(string effectScopeName)
            {
                if (string.IsNullOrEmpty(effectScopeName))
                {
                    return EffectScope.Default;
                }

                return Enum.TryParse(effectScopeName, ignoreCase: true, out EffectScope parsedScope)
                    ? parsedScope
                    : EffectScope.Default;
            }
        }

        // -------------------------------------------------------------------------
        // 1) Tree & identity — sempre usado (progressão, combate, UI)
        // -------------------------------------------------------------------------
        [Header("1 — Tree & identity")]
        [Tooltip("Must match the node id in skill_trees.json (e.g. f_t1_p1, f_t2_a1). " +
                 "For active skills this is also the skill id used by the combat simulator.")]
        [SerializeField] private string _nodeId = "";

        [Tooltip("Element this skill tree belongs to (Fire / Magma / Aether). Defines the tree column the node sits in.")]
        [FormerlySerializedAs("_treeElement")]
        [SerializeField] private ElementType _skillTreeElementCategory;

        [Tooltip("ON: this node is a passive (uses the passive fields below). " +
                 "OFF: this node is an active skill (uses the active skill fields below).")]
        [SerializeField] private bool _isPassiveNode;

        // -------------------------------------------------------------------------
        // 2) UI — painel da árvore / tooltips
        // -------------------------------------------------------------------------
        [Header("2 — UI")]
        [Tooltip("Player-facing display name. If empty, the node id is shown instead.")]
        [SerializeField] private string _displayName = "";

        [Tooltip("Player-facing description shown in the skill tree details panel.")]
        [TextArea(3, 12)]
        [SerializeField] private string _descriptionForUi = "";

        // -------------------------------------------------------------------------
        // 3) Active skill — combate (ignorado se passiva)
        // -------------------------------------------------------------------------
        [Header("3 — Active skill — core")]
        [Tooltip("Free-form label that classifies the active skill (e.g. Active, Innate). Defaults to \"Active\".")]
        [FormerlySerializedAs("_activeSkillType")]
        [SerializeField] private string _activeSkillTypeLabel = "Active";

        [Tooltip("Damage element for this active skill (None falls back to the actor's affinity).")]
        [FormerlySerializedAs("_activeElement")]
        [SerializeField] private ElementType _activeSkillDamageElement;

        [Tooltip("Inclusive minimum of the random base damage roll for this active skill (before mitigations and modifiers).")]
        [FormerlySerializedAs("_damageMin")]
        [SerializeField] private int _baseDamageMinimum;

        [Tooltip("Inclusive maximum of the random base damage roll for this active skill (before mitigations and modifiers).")]
        [FormerlySerializedAs("_damageMax")]
        [SerializeField] private int _baseDamageMaximum;

        [Tooltip("Probability (0..1) that a successful hit lands as a critical strike, before stat/tier modifiers.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("_baseCritChance")]
        [SerializeField] private double _baseCriticalHitChanceFraction;

        [Tooltip("Probability (0..1) that the skill lands at all, before being multiplied by the actor's accuracy stat.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("_accuracy")]
        [SerializeField] private double _baseHitAccuracyFraction = 1.0;

        [Tooltip("Who can be selected as target: OneEnemy, UpToThreeEnemies, AllEnemies, Self, OneAlly, SelfOrAlly, SelfAndAlly.")]
        [FormerlySerializedAs("_targetKind")]
        [SerializeField] private SkillTargetKind _targetSelectionKind = SkillTargetKind.OneEnemy;

        [Tooltip("Absolute probability (0..1) that the AI picks this skill when it is eligible. " +
                 "Default 1.0 = always picks if eligible. Lower values create variation between equally valid skills.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("_chanceToUse")]
        [SerializeField] private double _aiAbsoluteChanceToConsiderWhenEligible = 1.0;

        [Tooltip("HP gate on the actor: the AI only considers this skill while CurrentHp / MaxHp is below this fraction. " +
                 "Default 1.0 = no gate (always eligible regardless of HP).")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("_selfHpPercentBelow")]
        [SerializeField] private double _aiOnlyEligibleWhenOwnHpFractionBelow = 1.0;

        [Tooltip("Corruption added to the world meter when the player casts this skill (enemies do not pay this cost).")]
        [FormerlySerializedAs("_corruptionCost")]
        [SerializeField] private double _corruptionCostAddedWhenPlayerCasts = CorruptionRules.DefaultSkillCorruptionCost;

        [Header("3b — Active skill — effects")]
        [Tooltip("Effects applied to the target after a successful hit (DOTs, tokens, push/pull, heals, stuns).")]
        [FormerlySerializedAs("_effectsOnHit")]
        [SerializeField] private List<SerializableEffectSpec> _effectsAppliedAfterSuccessfulHit = new();

        // -------------------------------------------------------------------------
        // 4) Passive — combate (Game.Core) — ignorado se activa
        // -------------------------------------------------------------------------
        [Header("4 — Passive — combat (Core)")]
        [Tooltip("Which passive rule this node implements. The fields below are interpreted by this kind " +
                 "(see PassiveRuleApplier for the per-kind semantics).")]
        [SerializeField] private PassiveEffectKind _passiveEffectKind;

        [Tooltip("Skill id this passive watches for (e.g. for OutgoingDamageVsSkillId, the skill that gets the bonus). " +
                 "Match value is the node id of an active skill (= its skill id).")]
        [FormerlySerializedAs("_passiveSkillId")]
        [SerializeField] private string _passiveAppliesWhenSkillIdMatches = "";

        [Tooltip("Skill id that must have been used previously to prime this passive " +
                 "(e.g. OutgoingDamageAfterPrerequisiteSkill).")]
        [FormerlySerializedAs("_passivePrerequisiteSkillId")]
        [SerializeField] private string _passivePrerequisiteSkillIdThatMustBeUsedFirst = "";

        [Tooltip("ON enables 'Passive DoT Type Filter' below; OFF leaves the DoT type unset (passive ignores DoT type).")]
        [FormerlySerializedAs("_passiveUseDotType")]
        [SerializeField] private bool _passiveUsesDotTypeFilter;

        [Tooltip("DoT type the passive cares about (e.g. only buffs damage when the target has Bleed). " +
                 "Used only when 'Passive Uses DoT Type Filter' is ON.")]
        [FormerlySerializedAs("_passiveDotType")]
        [SerializeField] private DotType _passiveDotTypeFilter;

        [Tooltip("ON enables 'Passive Token Type Filter' below; OFF leaves it unset.")]
        [FormerlySerializedAs("_passiveUseTokenType")]
        [SerializeField] private bool _passiveUsesTokenTypeFilter;

        [Tooltip("Token type the passive cares about (e.g. penalty while the actor has Taunt). " +
                 "Used only when 'Passive Uses Token Type Filter' is ON.")]
        [FormerlySerializedAs("_passiveTokenType")]
        [SerializeField] private TokenType _passiveTokenTypeFilter;

        [Tooltip("ON enables 'Passive Token Type To Grant When Triggered' below.")]
        [FormerlySerializedAs("_passiveUseGrantTokenType")]
        [SerializeField] private bool _passiveGrantsExtraTokenOfType;

        [Tooltip("Token type granted by this passive when it fires (e.g. GrantTokenAtTurnStartIfCondition).")]
        [FormerlySerializedAs("_passiveGrantTokenType")]
        [SerializeField] private TokenType _passiveTokenTypeToGrantWhenTriggered;

        [Tooltip("ON enables 'Passive Required Token Type On Actor' below — passive only applies if actor has it.")]
        [FormerlySerializedAs("_passiveUseIfHasTokenType")]
        [SerializeField] private bool _passiveOnlyAppliesWhenActorHasTokenType;

        [Tooltip("Token type that must be present on the actor for this passive to apply. " +
                 "Used only when 'Passive Only Applies When Actor Has Token Type' is ON.")]
        [FormerlySerializedAs("_passiveIfHasTokenType")]
        [SerializeField] private TokenType _passiveRequiredTokenTypeOnActor;

        [Tooltip("ON enables 'Passive Blocking Token Type On Actor' below — passive is suppressed when actor has it.")]
        [FormerlySerializedAs("_passiveUseUnlessHasTokenType")]
        [SerializeField] private bool _passiveOnlyAppliesWhenActorLacksTokenType;

        [Tooltip("Token type whose presence on the actor blocks this passive. " +
                 "Used only when 'Passive Only Applies When Actor Lacks Token Type' is ON.")]
        [FormerlySerializedAs("_passiveUnlessHasTokenType")]
        [SerializeField] private TokenType _passiveBlockingTokenTypeOnActor;

        [Tooltip("Generic numeric magnitude used by most damage-modifier kinds.\n" +
                 "• OutgoingDamageVs* / OutgoingDamageAfterPrerequisiteSkill: additive damage bonus fraction (0.10 = +10%).\n" +
                 "• OutgoingDamagePenaltyWhenToken: signed additive (e.g. -0.12 = -12%).\n" +
                 "• IncomingDamageMultiplierWhenHpBelow: incoming damage MULTIPLIER (e.g. 0.88 = take 88% damage).\n" +
                 "• ExtraHealPercentOnSelfSkill: heal percentage of MaxHp (5 = 5%, not a fraction).\n" +
                 "• DotTickDamageBonusWhenTargetHpBelow: extra DoT tick multiplier added (0.20 = +20%).")]
        [FormerlySerializedAs("_passiveAdditive")]
        [SerializeField] private double _passiveDamageBonusOrIncomingMultiplierMagnitude;

        [Tooltip("Damage bonus fraction added per stack of the configured DoT on the target (e.g. 0.03 = +3% per stack). " +
                 "Used by OutgoingDamageVsDotOnTarget. Capped by 'Passive Damage Bonus Fraction Maximum Cap'.")]
        [FormerlySerializedAs("_passiveAdditivePerStack")]
        [SerializeField] private double _passiveDamageBonusFractionPerDotStackOnTarget;

        [Tooltip("Maximum total damage bonus fraction the per-stack accumulator can reach (e.g. 0.12 = +12% cap). " +
                 "Used together with 'Per Dot Stack' value above.")]
        [FormerlySerializedAs("_passiveCap")]
        [SerializeField] private double _passiveDamageBonusFractionMaximumCap;

        [Tooltip("HP fraction threshold (0..1) that activates the passive when actor/target HP drops below it " +
                 "(e.g. 0.5 = activates while HP < 50%). Used by IncomingDamageMultiplierWhenHpBelow and " +
                 "DotTickDamageBonusWhenTargetHpBelow.")]
        [Range(0f, 1f)]
        [FormerlySerializedAs("_passiveHpBelowPercent")]
        [SerializeField] private double _passiveActivatesWhenHpFractionBelow;

        [Tooltip("Primary integer parameter. Meaning depends on Passive Effect Kind:\n" +
                 "• GrantTokenAtTurnStartIfCondition / ExtraTokenOnSelfSkill: number of token stacks granted (min 1).\n" +
                 "• DotDurationBonus: extra turns added to the base DoT duration.\n" +
                 "• ApplyExtraDotAfterSkillIfTargetHasDot: potency (per-tick damage) of the extra DoT (default 2).")]
        [FormerlySerializedAs("_passiveIntValue")]
        [SerializeField] private int _passiveStacksOrDotPotencyOrTurnsBonusInteger;

        [Tooltip("Secondary integer parameter. Meaning depends on Passive Effect Kind:\n" +
                 "• DotDurationBonus: maximum total duration cap (after adding the bonus).\n" +
                 "• ApplyExtraDotAfterSkillIfTargetHasDot: base duration in turns of the extra DoT (default 2).\n" +
                 "Unused by other effect kinds.")]
        [FormerlySerializedAs("_passiveIntValue2")]
        [SerializeField] private int _passiveDotDurationOrMaxTurnCapInteger;

        // -------------------------------------------------------------------------
        // 5) Passive — apresentação Unity (event bus)
        // -------------------------------------------------------------------------
        [Header("5 — Passive — Unity presentation (optional)")]
        [Tooltip("List of passive triggers for which the Unity Event below fires (filter). " +
                 "Leave empty to fire on every trigger that is relevant to this passive.")]
        [FormerlySerializedAs("_onlyFireOnPassiveTriggers")]
        [SerializeField] private PassiveTrigger[] _unityEventFiresOnlyForThesePassiveTriggers = Array.Empty<PassiveTrigger>();

        [Tooltip("Unity Event invoked from MonoBehaviours when the passive's combat trigger fires (VFX, SFX, popups). " +
                 "Hook your scene listeners here.")]
        [FormerlySerializedAs("_whenPassiveDispatch")]
        [SerializeField] private UnityEvent _unityEventInvokedWhenPassiveTriggerFires = new();

        public string NodeId => _nodeId;
        public ElementType SkillTreeElementCategory => _skillTreeElementCategory;
        public bool IsPassiveNode => _isPassiveNode;

        public string DisplayName =>
            string.IsNullOrEmpty(_displayName) ? _nodeId : _displayName;

        public string DescriptionForUi => _descriptionForUi;

        public PassiveDefinition ToRuntimePassiveDefinition()
        {
            if (!_isPassiveNode || string.IsNullOrWhiteSpace(_nodeId))
            {
                throw new InvalidOperationException(
                    $"{name}: ToRuntimePassiveDefinition só para nós passivos com _nodeId.");
            }

            return new PassiveDefinition
            {
                Id = _nodeId,
                EffectKind = _passiveEffectKind,
                SkillId = string.IsNullOrEmpty(_passiveAppliesWhenSkillIdMatches)
                    ? null
                    : _passiveAppliesWhenSkillIdMatches,
                PrerequisiteSkillId = string.IsNullOrEmpty(_passivePrerequisiteSkillIdThatMustBeUsedFirst)
                    ? null
                    : _passivePrerequisiteSkillIdThatMustBeUsedFirst,
                DotType = _passiveUsesDotTypeFilter ? _passiveDotTypeFilter : null,
                TokenType = _passiveUsesTokenTypeFilter ? _passiveTokenTypeFilter : null,
                GrantTokenType = _passiveGrantsExtraTokenOfType ? _passiveTokenTypeToGrantWhenTriggered : null,
                IfHasTokenType = _passiveOnlyAppliesWhenActorHasTokenType ? _passiveRequiredTokenTypeOnActor : null,
                UnlessHasTokenType = _passiveOnlyAppliesWhenActorLacksTokenType ? _passiveBlockingTokenTypeOnActor : null,
                Additive = _passiveDamageBonusOrIncomingMultiplierMagnitude,
                AdditivePerStack = _passiveDamageBonusFractionPerDotStackOnTarget,
                Cap = _passiveDamageBonusFractionMaximumCap,
                HpBelowPercent = _passiveActivatesWhenHpFractionBelow,
                IntValue = _passiveStacksOrDotPotencyOrTurnsBonusInteger,
                IntValue2 = _passiveDotDurationOrMaxTurnCapInteger,
            };
        }

        public SkillDefinition ToRuntimeSkillDefinition()
        {
            if (_isPassiveNode || string.IsNullOrWhiteSpace(_nodeId))
            {
                throw new InvalidOperationException(
                    $"{name}: ToRuntimeSkillDefinition só para nós activos com _nodeId.");
            }

            var clampedDamageMaximum = Math.Max(_baseDamageMinimum, _baseDamageMaximum);
            var clampedDamageMinimum = Math.Min(_baseDamageMinimum, _baseDamageMaximum);

            return new SkillDefinition
            {
                Id = _nodeId,
                Name = DisplayName,
                Element = _activeSkillDamageElement,
                Type = string.IsNullOrEmpty(_activeSkillTypeLabel) ? "Active" : _activeSkillTypeLabel,
                BaseDamage = new DamageRange { Min = clampedDamageMinimum, Max = clampedDamageMaximum },
                BaseCritChance = _baseCriticalHitChanceFraction,
                Accuracy = _baseHitAccuracyFraction,
                TargetKind = _targetSelectionKind,
                EffectsOnHit = (_effectsAppliedAfterSuccessfulHit ?? Enumerable.Empty<SerializableEffectSpec>())
                    .Select(spec => spec.ToRuntimeSpec())
                    .ToList(),
                ChanceToUse = _aiAbsoluteChanceToConsiderWhenEligible,
                SelfHpPercentBelow = _aiOnlyEligibleWhenOwnHpFractionBelow,
                CorruptionCost = _corruptionCostAddedWhenPlayerCasts,
            };
        }

        internal bool ShouldDispatchUnityEventForPassiveTrigger(PassiveTrigger passiveTrigger)
        {
            if (!_isPassiveNode)
            {
                return false;
            }

            return _unityEventFiresOnlyForThesePassiveTriggers == null ||
                   _unityEventFiresOnlyForThesePassiveTriggers.Length == 0 ||
                   Array.IndexOf(_unityEventFiresOnlyForThesePassiveTriggers, passiveTrigger) >= 0;
        }

        internal void InvokeUnityEventForPassiveDispatch() => _unityEventInvokedWhenPassiveTriggerFires.Invoke();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_baseDamageMaximum < _baseDamageMinimum)
            {
                var swap = _baseDamageMinimum;
                _baseDamageMinimum = _baseDamageMaximum;
                _baseDamageMaximum = swap;
            }
        }
#endif
    }
}
