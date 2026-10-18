using Erumperem.Characters;
using UnityEngine;
using Services.DebugUtilities;

namespace Core.CharacterStats.Testing
{
    /// <summary>
    /// Editor-only test harness for CharacterStatsRepository: dump a
    /// character's base / saved / current stats, apply a single test modifier
    /// (checking the 50% rule first), or delete the saved data — without
    /// needing an actual item asset.
    /// </summary>
    public sealed class CharacterStatsTestbed : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private AllyCharacterStatDefinition _target;

        [Header("Test Modifier")]
        [SerializeField] private IntensityLibrary _intensityLibrary;
        [SerializeField] private StatType _statType;
        [SerializeField] private StatModifierType _modifierType;
        [SerializeField] private StatIntensity _intensity = StatIntensity.Plus1;

        public void DumpStats()
        {
            if (_target == null) { Log(LogLevel.Error, "Target not assigned."); return; }

            var saved = CharacterStatsRepository.GetStatsForCharacter(_target.CharacterId);

            foreach (StatType type in System.Enum.GetValues(typeof(StatType)))
            {
                float baseValue = CharacterStatResolver.GetBase(_target, type);
                float delta = saved.Get(type);
                Log(LogLevel.Debug, $"[{_target.CharacterId}] {type}: base={baseValue} saved={delta} current={baseValue + delta}");
            }
        }

        public void ApplyTestModifier()
        {
            if (_target == null) { Log(LogLevel.Error, "Target not assigned."); return; }
            if (_intensityLibrary == null) { Log(LogLevel.Error, "IntensityLibrary not assigned."); return; }

            var modifiers = new[] { new StatModifierEntry(_statType, _modifierType, _intensity).Resolve(_intensityLibrary) };

            var saved = CharacterStatsRepository.GetStatsForCharacter(_target.CharacterId);
            var check = StatApplicabilityRule.Evaluate(_target, saved, modifiers);
            if (!check.CanApply)
            {
                Log(LogLevel.Warning, $"Refused by applicability rule: {check.Reason}");
                return;
            }

            CharacterStatsRepository.ApplyModifiers(_target, modifiers);
            DumpStats();
        }

        /// <summary>Deletes the whole shared file (every character).</summary>
        public void DeleteFile() => CharacterStatsRepository.DeleteFile();

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[CharacterStatsTestbed] {msg}", LogCategory.Inventory);
    }
}
