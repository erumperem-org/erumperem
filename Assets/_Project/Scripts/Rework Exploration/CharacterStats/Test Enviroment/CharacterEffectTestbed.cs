using UnityEngine;
using Services.DebugUtilities;

namespace Core.CharacterStats.Testing
{
    /// <summary>
    /// Editor-only test harness for CharacterStatsRepository: dump a
    /// character's current stats, apply a single test modifier, or delete
    /// the shared file — without needing an actual item asset.
    /// </summary>
    public sealed class CharacterStatsTestbed : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private string _characterId = "PLACEHOLDER_CHARACTER_ID";

        [Header("Test Modifier")]
        [SerializeField] private StatType _statType;
        [SerializeField] private StatModifierType _modifierType;
        [SerializeField] private float _magnitude = 1f;

        public void DumpStats()
        {
            var stats = CharacterStatsRepository.GetStatsForCharacter(_characterId);

            foreach (StatType type in System.Enum.GetValues(typeof(StatType)))
                Log(LogLevel.Debug, $"[{_characterId}] {type} = {stats.Get(type)}");
        }

        public void ApplyTestModifier()
        {
            var modifiers = new[] { new StatModifierEntry(_statType, _modifierType, _magnitude) };

            CharacterStatsRepository.ApplyModifiers(_characterId, modifiers);
            DumpStats();
        }

        public void DeleteFile() => CharacterStatsRepository.DeleteFile();

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[CharacterStatsTestbed] {msg}", LogCategory.Inventory);
    }
}