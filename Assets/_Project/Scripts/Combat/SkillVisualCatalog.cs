using System;
using System.Collections.Generic;
using UnityEngine;

namespace Erumperem.Combat
{
    /// <summary>
    /// Maps <see cref="Game.Core.Models.SkillDefinition.Id"/> to UI sprites for combat skill buttons and tooltips.
    /// </summary>
    [CreateAssetMenu(menuName = "Erumperem/Combat/Skill Visual Catalog", fileName = "SkillVisualCatalog")]
    public sealed class SkillVisualCatalog : ScriptableObject
    {
        [SerializeField] private List<SkillVisualDefinition> entries = new();

        private readonly Dictionary<string, SkillVisualDefinition> _runtimeLookupBySkillId =
            new(StringComparer.Ordinal);

        public IReadOnlyList<SkillVisualDefinition> Entries => entries;

        private void OnEnable() => RebuildLookup();

        private void OnValidate()
        {
            if (entries == null)
            {
                entries = new List<SkillVisualDefinition>();
            }

            RebuildLookup();
        }

        private void RebuildLookup()
        {
            _runtimeLookupBySkillId.Clear();
            foreach (var entry in entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.SkillId))
                {
                    continue;
                }

                _runtimeLookupBySkillId[entry.SkillId] = entry;
            }
        }

        public bool TryGet(string skillId, out SkillVisualDefinition definition)
        {
            if (_runtimeLookupBySkillId.Count == 0)
            {
                RebuildLookup();
            }

            if (string.IsNullOrWhiteSpace(skillId))
            {
                definition = null;
                return false;
            }

            return _runtimeLookupBySkillId.TryGetValue(skillId, out definition);
        }
    }

    [Serializable]
    public sealed class SkillVisualDefinition
    {
        [Tooltip("Must match skills.json / SkillDefinition.Id (e.g. f_t1_a1).")]
        public string skillId = "";

        [Tooltip("Optional label for editor lists; not shown on the combat button.")]
        public string displayName = "";

        public Sprite icon;

        public Color iconColor = Color.white;

        public string SkillId => skillId ?? string.Empty;
    }
}
