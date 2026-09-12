using System;
using UnityEngine;

namespace Core.CharacterStats
{
    /// <summary>
    /// A single stat change carried by an item. Multi-status ("thematic")
    /// items configure more than one of these — one per affected stat.
    /// Magnitude values are authored per item asset in the Inspector; this
    /// project intentionally does not hardcode intensity tiers ("+", "++",
    /// "+++") to numbers in code, since that is a game-balance decision.
    /// </summary>
    [Serializable]
    public struct StatModifierEntry
    {
        [SerializeField] private StatType _statType;
        [SerializeField] private StatModifierType _modifierType;
        [SerializeField] private float _magnitude;

        public StatType StatType => _statType;
        public StatModifierType ModifierType => _modifierType;
        public float Magnitude => _magnitude;

        /// <summary>
        /// Public constructor for code that needs to build an entry directly
        /// (e.g. editor testbeds). Item assets normally configure entries via
        /// the Inspector instead of this constructor.
        /// </summary>
        public StatModifierEntry(StatType statType, StatModifierType modifierType, float magnitude)
        {
            _statType = statType;
            _modifierType = modifierType;
            _magnitude = magnitude;
        }
    }
}
