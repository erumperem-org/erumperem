using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Core.CharacterStats
{
    /// <summary>
    /// Single source of truth that maps (stat, modifier type, intensity symbol)
    /// to a concrete number. Items store only the symbol (<see cref="StatIntensity"/>);
    /// changing a number here changes it in ALL items that use that symbol.
    ///
    /// Value semantics follow the modifier type:
    ///  - Absolute: signed flat delta in the stat's own unit (HP points, or
    ///    fractions like 0.05 for Defense/Critical).
    ///  - Relative: signed percentage n (applied as n/100).
    ///
    /// Storage is one flat float[] (stat × type × 8 intensity slots), edited
    /// through a table inspector (IntensityLibraryEditor). Every combination
    /// always exists — no missing or duplicate rows. Values start at 0:
    /// this project deliberately does not invent balance numbers.
    ///
    /// Create via: Assets → Create → Character Stats → Intensity Library
    /// </summary>
    [CreateAssetMenu(menuName = "Character Stats/Intensity Library", fileName = "IntensityLibrary")]
    public sealed class IntensityLibrary : ScriptableObject
    {
        public const int SlotCount = 8;   // Minus4..Minus1, Plus1..Plus4
        private const int TypeCount = 2;  // Absolute, Relative

        [SerializeField, HideInInspector] private float[] _values = Array.Empty<float>();

        // Legacy list layout from the first version; migrated into _values and cleared.
        [Serializable]
        private struct LegacyRow
        {
            public StatType Stat;
            public StatModifierType ModifierType;
            public StatIntensity Intensity;
            public float Value;
        }

        [SerializeField, HideInInspector] private List<LegacyRow> _rows = new();

        private void OnEnable() => EnsureLayout();
        private void OnValidate() => EnsureLayout();

        /// <summary>The number for the given symbol (0 for None).</summary>
        public float Resolve(StatType stat, StatModifierType type, StatIntensity intensity)
        {
            EnsureLayout();

            int index = IndexOf(stat, type, intensity);
            return index >= 0 && index < _values.Length ? _values[index] : 0f;
        }

        /// <summary>Sets one value (used by generators and tools). No-op for None.</summary>
        public void SetValue(StatType stat, StatModifierType type, StatIntensity intensity, float value)
        {
            EnsureLayout();

            int index = IndexOf(stat, type, intensity);
            if (index < 0 || index >= _values.Length) return;

            _values[index] = value;

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }

        /// <summary>Flat-array index of a combination, or -1 for None. Also used by the table inspector.</summary>
        public static int IndexOf(StatType stat, StatModifierType type, StatIntensity intensity)
        {
            int slot = SlotOf(intensity);
            if (slot < 0) return -1;

            return (((int)stat * TypeCount) + (int)type) * SlotCount + slot;
        }

        /// <summary>
        /// Keeps the array sized to the current StatType enum (new stats are
        /// appended, existing values keep their index) and migrates the legacy list.
        /// </summary>
        public void EnsureLayout()
        {
            int required = Enum.GetValues(typeof(StatType)).Length * TypeCount * SlotCount;

            if (_values == null)
                _values = new float[required];
            else if (_values.Length < required)
                Array.Resize(ref _values, required);

            if (_rows != null && _rows.Count > 0)
            {
                foreach (var row in _rows)
                {
                    int index = IndexOf(row.Stat, row.ModifierType, row.Intensity);
                    if (index >= 0 && index < _values.Length)
                        _values[index] = row.Value;
                }

                _rows.Clear();

#if UNITY_EDITOR
                EditorUtility.SetDirty(this);
#endif
            }
        }

        private static int SlotOf(StatIntensity intensity)
        {
            switch (intensity)
            {
                case StatIntensity.Minus4: return 0;
                case StatIntensity.Minus3: return 1;
                case StatIntensity.Minus2: return 2;
                case StatIntensity.Minus1: return 3;
                case StatIntensity.Plus1:  return 4;
                case StatIntensity.Plus2:  return 5;
                case StatIntensity.Plus3:  return 6;
                case StatIntensity.Plus4:  return 7;
                default:                   return -1;
            }
        }
    }
}