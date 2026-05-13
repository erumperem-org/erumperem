using System;
using System.Collections.Generic;
using Game.Core.Domain;
using UnityEngine;

namespace Erumperem.Combat.Tokens
{
    /// <summary>
    /// Presentation data for combat tokens (<see cref="TokenType"/>) and DOT debuffs (<see cref="DotType"/>).
    /// Single catalog keeps icon/name/color mapping in one asset (plan option B).
    /// </summary>
    [CreateAssetMenu(menuName = "Erumperem/Combat/Token Visual Catalog", fileName = "TokenVisualCatalog")]
    public sealed class TokenVisualCatalog : ScriptableObject
    {
        [SerializeField] private List<TokenVisualDefinition> entries = new();

        [SerializeField] private List<DotVisualDefinition> dotEntries = new();

        /// <summary>Order preserved for left-to-right strip display among visible types.</summary>
        public IReadOnlyList<TokenVisualDefinition> Entries => entries;

        /// <summary>DOT rows (Bleed, etc.) — separate from <see cref="TokenType"/> rows.</summary>
        public IReadOnlyList<DotVisualDefinition> DotEntries => dotEntries;

        private readonly Dictionary<TokenType, TokenVisualDefinition> _runtimeLookup = new();
        private readonly Dictionary<DotType, DotVisualDefinition> _runtimeDotLookup = new();

        private void OnEnable()
        {
            EnsureAllTokenTypesRepresented();
            EnsureAllDotTypesRepresented();
            RebuildLookup();
            RebuildDotLookup();
        }

        private void OnValidate()
        {
            if (entries == null)
            {
                entries = new List<TokenVisualDefinition>();
            }

            if (dotEntries == null)
            {
                dotEntries = new List<DotVisualDefinition>();
            }

            EnsureAllTokenTypesRepresented();
            EnsureAllDotTypesRepresented();
            RebuildLookup();
            RebuildDotLookup();
        }

        private void Reset()
        {
            entries = new List<TokenVisualDefinition>();
            dotEntries = new List<DotVisualDefinition>();
            EnsureAllTokenTypesRepresented();
            EnsureAllDotTypesRepresented();
            RebuildLookup();
            RebuildDotLookup();
        }

        private void EnsureAllTokenTypesRepresented()
        {
            var seen = new HashSet<TokenType>();
            foreach (var definition in entries)
            {
                if (definition != null)
                {
                    seen.Add(definition.TokenType);
                }
            }

            foreach (TokenType tokenType in Enum.GetValues(typeof(TokenType)))
            {
                if (seen.Contains(tokenType))
                {
                    continue;
                }

                entries.Add(new TokenVisualDefinition { tokenType = tokenType, displayName = tokenType.ToString() });
            }
        }

        private void EnsureAllDotTypesRepresented()
        {
            var seen = new HashSet<DotType>();
            foreach (var definition in dotEntries)
            {
                if (definition != null)
                {
                    seen.Add(definition.DotType);
                }
            }

            foreach (DotType dotType in Enum.GetValues(typeof(DotType)))
            {
                if (seen.Contains(dotType))
                {
                    continue;
                }

                dotEntries.Add(new DotVisualDefinition { dotType = dotType, displayName = dotType.ToString() });
            }
        }

        private void RebuildLookup()
        {
            _runtimeLookup.Clear();
            foreach (var definition in entries)
            {
                if (definition == null)
                {
                    continue;
                }

                _runtimeLookup[definition.TokenType] = definition;
            }
        }

        private void RebuildDotLookup()
        {
            _runtimeDotLookup.Clear();
            foreach (var definition in dotEntries)
            {
                if (definition == null)
                {
                    continue;
                }

                _runtimeDotLookup[definition.DotType] = definition;
            }
        }

        public bool TryGet(TokenType tokenType, out TokenVisualDefinition definition)
        {
            if (_runtimeLookup.Count == 0)
            {
                RebuildLookup();
            }

            return _runtimeLookup.TryGetValue(tokenType, out definition);
        }

        public bool TryGetDot(DotType dotType, out DotVisualDefinition definition)
        {
            if (_runtimeDotLookup.Count == 0)
            {
                RebuildDotLookup();
            }

            return _runtimeDotLookup.TryGetValue(dotType, out definition);
        }
    }

    [Serializable]
    public sealed class DotVisualDefinition
    {
        public DotType dotType;

        public string displayName = "";

        public Sprite icon;

        public Color iconColor = Color.white;

        public Color backgroundTint = Color.white;

        public DotType DotType => dotType;
    }

    [Serializable]
    public sealed class TokenVisualDefinition
    {
        [Tooltip("Must match Game.Core token identity.")]
        public TokenType tokenType;

        [Tooltip("Shown next to icon when stacks > 1; optional short name for tooltips.")]
        public string displayName = "";

        public Sprite icon;

        public Color iconColor = Color.white;

        public Color backgroundTint = Color.white;

        public TokenType TokenType => tokenType;
    }
}
