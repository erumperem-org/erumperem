using System;
using System.Collections.Generic;
using Core.Exploration.Items;
using Erumperem.Combat;
using Game.Core.Domain;
using Game.Core.Items;
using UnityEngine;

namespace Erumperem.Combat.Authoring
{
    /// <summary>
    /// Designer authoring surface for one combat item. Duplicate this asset, fill the Inspector,
    /// then run Erumperem/Combat/Export Catalog. Runtime stats use StreamingAssets items.json.
    /// Rarity only changes the icon background color; IconFamilyId shares the same sprite across sizes.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatItem", menuName = "Erumperem/Combat/Item")]
    public sealed class CombatItemAsset : ScriptableObject, IItem
    {
        [Serializable]
        public sealed class SerializableStatModifier
        {
            public CombatItemStatKind Stat;
            public CombatItemModifierScale Scale;
            public int IntensityRank = 1;

            public CombatItemStatModifier ToRuntimeModifier() =>
                new()
                {
                    Stat = Stat,
                    Scale = Scale,
                    IntensityRank = IntensityRank,
                };
        }

        [SerializeField] private string _itemId;
        [SerializeField] private string _displayName;
        [TextArea(2, 6)]
        [SerializeField] private string _description;
        [SerializeField] private CombatItemRarity _rarity = CombatItemRarity.Common;
        [SerializeField] private CombatItemKind _kind = CombatItemKind.IndividualStatus;
        [Tooltip("Same sprite family for Small/Medium/Big. Rarity only tints the slot background.")]
        [SerializeField] private string _iconFamilyId;
        [SerializeField] private CombatItemUtilityKind _utilityKind = CombatItemUtilityKind.None;
        [SerializeField] private List<SerializableStatModifier> _statModifiers = new();
        [SerializeField] private Sprite _icon;
        [SerializeField] private StorageMode _storageMode = StorageMode.Stackable;

        public string ItemId => _itemId;
        public string DisplayName => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;
        public string Description => _description;
        public CombatItemRarity Rarity => _rarity;
        public CombatItemKind Kind => _kind;
        public string IconFamilyId => _iconFamilyId;
        public CombatItemUtilityKind UtilityKind => _utilityKind;
        public StorageMode storageMode => _storageMode;
        public Sprite Sprite => _icon;

        public CombatItemDefinition ToRuntimeDefinition()
        {
            var modifiers = new List<CombatItemStatModifier>();
            if (_statModifiers != null)
            {
                foreach (var modifier in _statModifiers)
                {
                    if (modifier != null)
                    {
                        modifiers.Add(modifier.ToRuntimeModifier());
                    }
                }
            }

            return new CombatItemDefinition
            {
                Id = _itemId?.Trim() ?? string.Empty,
                DisplayName = DisplayName,
                Description = _description ?? string.Empty,
                Rarity = _rarity,
                Kind = _kind,
                IconFamilyId = string.IsNullOrWhiteSpace(_iconFamilyId) ? _itemId : _iconFamilyId,
                StatModifiers = modifiers,
                UtilityKind = _utilityKind,
            };
        }

        public void ExecuteItemEffect()
        {
            CombatItemRuntimeService.ExecuteFromInventory(this);
        }

        public void ApplyFactoryDefinition(CombatItemDefinition definition)
        {
            _itemId = definition.Id;
            _displayName = definition.DisplayName;
            _description = definition.Description;
            _rarity = definition.Rarity;
            _kind = definition.Kind;
            _iconFamilyId = definition.IconFamilyId;
            _utilityKind = definition.UtilityKind;
            _storageMode = definition.Kind == CombatItemKind.Utility
                ? StorageMode.Unique
                : StorageMode.Stackable;
            _statModifiers = new List<SerializableStatModifier>();
            foreach (var modifier in definition.StatModifiers)
            {
                _statModifiers.Add(new SerializableStatModifier
                {
                    Stat = modifier.Stat,
                    Scale = modifier.Scale,
                    IntensityRank = modifier.IntensityRank,
                });
            }
        }
    }
}
