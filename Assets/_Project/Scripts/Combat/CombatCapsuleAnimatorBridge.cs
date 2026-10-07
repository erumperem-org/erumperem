using System;
using System.Collections.Generic;
using UnityEngine;

namespace Erumperem.Combat
{
    [Serializable]
    public sealed class CombatSkillAttackAnimationBinding
    {
        [Tooltip("Must match SkillDefinition.Id (e.g. buck_innate_active2).")]
        public string skillId = "";

        [Tooltip("Animator trigger name on this capsule (e.g. QuickDraw, ShotgunShot, RifleShot).")]
        public string attackAnimatorTrigger = "Attack";
    }

    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(CombatCapsuleTag))]
    public class CombatCapsuleAnimatorBridge : MonoBehaviour
    {
        [Header("Referências")]
        [SerializeField] private CombatSessionHub combatSessionHub;

        [Header("Skill → attack animation")]
        [Tooltip("Fallback trigger when the skill has no binding (generic Attack).")]
        [SerializeField] private string defaultAttackAnimatorTrigger = "Attack";

        [Tooltip("Per-skill attack triggers for this battle prefab (Buck: QuickDraw / ShotgunShot / RifleShot).")]
        [SerializeField] private List<CombatSkillAttackAnimationBinding> skillAttackAnimationBindings = new();

        private Animator _animator;
        private CombatCapsuleTag _capsuleTag;
        private readonly Dictionary<string, int> _attackTriggerHashBySkillId =
            new(StringComparer.Ordinal);
        private int _defaultAttackTriggerHash;
        private static readonly int HitTakenTrigger = Animator.StringToHash("HitTaken");
        private static readonly int DeathTrigger = Animator.StringToHash("Death");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _capsuleTag = GetComponent<CombatCapsuleTag>();
            RebuildSkillAttackTriggerLookup();

            if (combatSessionHub == null)
            {
                combatSessionHub = FindFirstObjectByType<CombatSessionHub>();
            }
        }

        private void OnValidate()
        {
            RebuildSkillAttackTriggerLookup();
        }

        private void OnEnable()
        {
            if (combatSessionHub == null)
            {
                return;
            }

            combatSessionHub.OnCombatSkillExecutionPresentationStarted += HandleSkillStarted;
            combatSessionHub.OnCombatantPresentationDeath += HandleDeath;
        }

        private void OnDisable()
        {
            if (combatSessionHub == null)
            {
                return;
            }

            combatSessionHub.OnCombatSkillExecutionPresentationStarted -= HandleSkillStarted;
            combatSessionHub.OnCombatantPresentationDeath -= HandleDeath;
        }

        private void HandleSkillStarted(string actorCombatantId, string targetCombatantId, string skillId)
        {
            var capsuleCombatantId = _capsuleTag.combatantId;

            if (actorCombatantId == capsuleCombatantId)
            {
                FireAttackTriggerForSkill(skillId);
                return;
            }

            if (targetCombatantId == capsuleCombatantId)
            {
                _animator.SetTrigger(HitTakenTrigger);
            }
        }

        private void HandleDeath(string deadCombatantId)
        {
            if (deadCombatantId == _capsuleTag.combatantId)
            {
                _animator.SetTrigger(DeathTrigger);
            }
        }

        private void FireAttackTriggerForSkill(string skillId)
        {
            if (_animator == null)
            {
                return;
            }

            var attackTriggerHash = _defaultAttackTriggerHash;
            if (!string.IsNullOrWhiteSpace(skillId)
                && _attackTriggerHashBySkillId.TryGetValue(skillId, out var skillSpecificTriggerHash))
            {
                attackTriggerHash = skillSpecificTriggerHash;
            }

            _animator.SetTrigger(attackTriggerHash);
        }

        private void RebuildSkillAttackTriggerLookup()
        {
            _attackTriggerHashBySkillId.Clear();
            var fallbackTriggerName = string.IsNullOrWhiteSpace(defaultAttackAnimatorTrigger)
                ? "Attack"
                : defaultAttackAnimatorTrigger.Trim();
            _defaultAttackTriggerHash = Animator.StringToHash(fallbackTriggerName);

            if (skillAttackAnimationBindings == null)
            {
                return;
            }

            foreach (var binding in skillAttackAnimationBindings)
            {
                if (binding == null
                    || string.IsNullOrWhiteSpace(binding.skillId)
                    || string.IsNullOrWhiteSpace(binding.attackAnimatorTrigger))
                {
                    continue;
                }

                _attackTriggerHashBySkillId[binding.skillId.Trim()] =
                    Animator.StringToHash(binding.attackAnimatorTrigger.Trim());
            }
        }
    }
}
