using System;
using UnityEngine;

namespace Core.CharacterStats
{
    /// <summary>
    /// A single authored stat change carried by an item: WHICH stat, HOW it
    /// is applied (Absolute/Relative) and HOW STRONG (+, ++, -- ...). It holds
    /// no numbers — numbers come from <see cref="IntensityLibrary"/> via
    /// <see cref="Resolve"/>.
    /// </summary>
    [Serializable]
    public struct StatModifierEntry
    {
        [SerializeField] private StatType _statType;
        [SerializeField] private StatModifierType _modifierType;
        [SerializeField] private StatIntensity _intensity;

        public StatType StatType => _statType;
        public StatModifierType ModifierType => _modifierType;
        public StatIntensity Intensity => _intensity;

        public StatModifierEntry(StatType statType, StatModifierType modifierType, StatIntensity intensity)
        {
            _statType = statType;
            _modifierType = modifierType;
            _intensity = intensity;
        }

        public ResolvedStatModifier Resolve(IntensityLibrary library)
        {
            float value = library != null
                ? library.Resolve(_statType, _modifierType, _intensity)
                : 0f;

            return new ResolvedStatModifier(_statType, _modifierType, value);
        }
    }
}
