using UnityEngine;
using Core.Exploration.Items;

namespace Core.CharacterStats
{
    /// <summary>
    /// Skill tree reset item. Effect is intentionally empty for now — the
    /// real skill tree system does not exist yet. The intended call is left
    /// commented below, ready to be wired in once that system is available.
    ///
    /// Create via: Assets → Create → Character Stats → Skill Tree Reset Item
    /// </summary>
    [CreateAssetMenu(menuName = "Character Stats/Skill Tree Reset Item", fileName = "SkillTreeReset_")]
    public sealed class SkillTreeResetItem : ItemDefinition
    {
        [SerializeField] private SkillTreeResetScope _scope = SkillTreeResetScope.General;

        [Tooltip("Only used when Scope is CharacterSpecific. Placeholder id, same caveat as CharacterIdentityResolver.")]
        [SerializeField] private string _targetCharacterId;

        public SkillTreeResetScope Scope => _scope;
        public string TargetCharacterId => _targetCharacterId;

        public override void ExecuteItemEffect()
        {
            // Empty on purpose — the real skill tree system does not exist yet.
            //
            // TODO: once the skill tree system is available, call it here, e.g.:
            //
            // if (_scope == SkillTreeResetScope.General)
            //     SkillTreeSystem.Instance.ResetGeneralTree();
            // else
            //     SkillTreeSystem.Instance.ResetCharacterTree(_targetCharacterId);
        }
    }
}
