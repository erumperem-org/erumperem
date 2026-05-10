using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Config;
using Game.Core.Domain;
using Game.Core.Models;
using Game.Core.Passives;
using UnityEngine;
using UnityEngine.Events;

namespace Erumperem.Progression
{
    /// <summary>
    /// Single ScriptableObject per skill-tree node (passive or active): tree identity, UI, full combat data,
    /// and optional Unity passive presentation hooks. Use as the only authoring surface for this node.
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
            public string EffectScope = "Default";
            public bool UseToken;
            public TokenType Token;
            public bool UseDot;
            public DotType Dot;

            public EffectSpec ToRuntimeSpec() => new()
            {
                Type = Type,
                Chance = Chance,
                Stacks = Stacks,
                Potency = Potency,
                Duration = Duration,
                Steps = Steps,
                EffectScope = string.IsNullOrEmpty(EffectScope) ? "Default" : EffectScope,
                Token = UseToken ? Token : null,
                Dot = UseDot ? Dot : null,
            };
        }

        // -------------------------------------------------------------------------
        // 1) Tree & identity — sempre usado (progressão, combate, UI)
        // -------------------------------------------------------------------------
        [Header("1 — Tree & identity")]
        [Tooltip("Must match node id in skill_trees.json (e.g. f_t1_p1, f_t2_a1). Skill id for actives = este valor.")]
        [SerializeField] private string _nodeId = "";

        [SerializeField] private ElementType _treeElement;

        [Tooltip("Passive = regras PassiveDefinition + hooks. Active = SkillDefinition para o combate.")]
        [SerializeField] private bool _isPassiveNode;

        // -------------------------------------------------------------------------
        // 2) UI — painel da árvore / tooltips
        // -------------------------------------------------------------------------
        [Header("2 — UI")]
        [SerializeField] private string _displayName = "";

        [TextArea(3, 12)]
        [SerializeField] private string _descriptionForUi = "";

        // -------------------------------------------------------------------------
        // 3) Active skill — combate (ignorado se passiva)
        // -------------------------------------------------------------------------
        [Header("3 — Active skill — core")]
        [SerializeField] private string _activeSkillType = "Active";

        [SerializeField] private ElementType _activeElement;

        [SerializeField] private int _damageMin;
        [SerializeField] private int _damageMax;

        [Range(0f, 1f)]
        [SerializeField] private double _baseCritChance;

        [SerializeField] private double _accuracy = 1.0;

        [SerializeField] private SkillTargetKind _targetKind = SkillTargetKind.Enemy;

        [SerializeField] private int _weight = 1;

        [SerializeField] private double _corruptionCost = CorruptionRules.DefaultSkillCorruptionCost;

        [Header("3b — Active skill — effects")]
        [SerializeField] private List<SerializableEffectSpec> _effectsOnHit = new();
        [SerializeField] private List<SerializableEffectSpec> _comboBonus = new();

        // -------------------------------------------------------------------------
        // 4) Passive — combate (Game.Core) — ignorado se activa
        // -------------------------------------------------------------------------
        [Header("4 — Passive — combat (Core)")]
        [SerializeField] private PassiveEffectKind _passiveEffectKind;
        [SerializeField] private string _passiveSkillId = "";
        [SerializeField] private string _passivePrerequisiteSkillId = "";

        [SerializeField] private bool _passiveUseDotType;
        [SerializeField] private DotType _passiveDotType;

        [SerializeField] private bool _passiveUseTokenType;
        [SerializeField] private TokenType _passiveTokenType;

        [SerializeField] private bool _passiveUseGrantTokenType;
        [SerializeField] private TokenType _passiveGrantTokenType;

        [SerializeField] private bool _passiveUseIfHasTokenType;
        [SerializeField] private TokenType _passiveIfHasTokenType;

        [SerializeField] private bool _passiveUseUnlessHasTokenType;
        [SerializeField] private TokenType _passiveUnlessHasTokenType;

        [SerializeField] private double _passiveAdditive;
        [SerializeField] private double _passiveAdditivePerStack;
        [SerializeField] private double _passiveCap;
        [SerializeField] private double _passiveHpBelowPercent;
        [SerializeField] private int _passiveIntValue;
        [SerializeField] private int _passiveIntValue2;

        // -------------------------------------------------------------------------
        // 5) Passive — apresentação Unity (event bus)
        // -------------------------------------------------------------------------
        [Header("5 — Passive — Unity presentation (optional)")]
        [Tooltip("If empty, fires for every passive trigger once relevance checks pass.")]
        [SerializeField] private PassiveTrigger[] _onlyFireOnPassiveTriggers = Array.Empty<PassiveTrigger>();

        [SerializeField] private UnityEvent _whenPassiveDispatch = new();

        public string NodeId => _nodeId;
        public ElementType TreeElement => _treeElement;
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
                SkillId = string.IsNullOrEmpty(_passiveSkillId) ? null : _passiveSkillId,
                PrerequisiteSkillId = string.IsNullOrEmpty(_passivePrerequisiteSkillId)
                    ? null
                    : _passivePrerequisiteSkillId,
                DotType = _passiveUseDotType ? _passiveDotType : null,
                TokenType = _passiveUseTokenType ? _passiveTokenType : null,
                GrantTokenType = _passiveUseGrantTokenType ? _passiveGrantTokenType : null,
                IfHasTokenType = _passiveUseIfHasTokenType ? _passiveIfHasTokenType : null,
                UnlessHasTokenType = _passiveUseUnlessHasTokenType ? _passiveUnlessHasTokenType : null,
                Additive = _passiveAdditive,
                AdditivePerStack = _passiveAdditivePerStack,
                Cap = _passiveCap,
                HpBelowPercent = _passiveHpBelowPercent,
                IntValue = _passiveIntValue,
                IntValue2 = _passiveIntValue2,
            };
        }

        public SkillDefinition ToRuntimeSkillDefinition()
        {
            if (_isPassiveNode || string.IsNullOrWhiteSpace(_nodeId))
            {
                throw new InvalidOperationException(
                    $"{name}: ToRuntimeSkillDefinition só para nós activos com _nodeId.");
            }

            var damageMax = Math.Max(_damageMin, _damageMax);
            var damageMin = Math.Min(_damageMin, _damageMax);

            return new SkillDefinition
            {
                Id = _nodeId,
                Name = DisplayName,
                Element = _activeElement,
                Type = string.IsNullOrEmpty(_activeSkillType) ? "Active" : _activeSkillType,
                BaseDamage = new DamageRange { Min = damageMin, Max = damageMax },
                BaseCritChance = _baseCritChance,
                Accuracy = _accuracy,
                TargetKind = _targetKind,
                EffectsOnHit = (_effectsOnHit ?? Enumerable.Empty<SerializableEffectSpec>())
                    .Select(spec => spec.ToRuntimeSpec())
                    .ToList(),
                ComboBonus = (_comboBonus ?? Enumerable.Empty<SerializableEffectSpec>())
                    .Select(spec => spec.ToRuntimeSpec())
                    .ToList(),
                Weight = _weight,
                CorruptionCost = _corruptionCost,
            };
        }

        internal bool ShouldFirePassiveTrigger(PassiveTrigger trigger)
        {
            if (!_isPassiveNode)
            {
                return false;
            }

            return _onlyFireOnPassiveTriggers == null ||
                   _onlyFireOnPassiveTriggers.Length == 0 ||
                   Array.IndexOf(_onlyFireOnPassiveTriggers, trigger) >= 0;
        }

        internal void InvokePassiveDispatch() => _whenPassiveDispatch.Invoke();

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_damageMax < _damageMin)
            {
                var swap = _damageMin;
                _damageMin = _damageMax;
                _damageMax = swap;
            }
        }
#endif
    }
}
