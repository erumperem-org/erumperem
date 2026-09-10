using System;
using System.IO;
using System.Threading.Tasks;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Shop
{
    /// <summary>
    /// Persists/restores a SkillPointShopButton's tier progression.
    /// Different from ItemShopButton (which is stateless), this button's
    /// progress must survive across sessions to remain coherent.
    /// </summary>
    public sealed class SkillPointShopSaveSystem : MonoBehaviour
    {
        [SerializeField] private SkillPointShopButton _button;
        [SerializeField] private string _fileName = "skillpoint_shop.json";

        private string FullPath => Path.Combine(Application.persistentDataPath, _fileName);

        public async void SaveAsync()
        {
            if (_button == null) { Log(LogLevel.Error, "SkillPointShopButton not assigned."); return; }

            var data = new SkillPointShopSaveData { GlobalTierIndex = _button.GlobalTierIndex };
            string json = JsonUtility.ToJson(data, prettyPrint: true);

            try
            {
                await File.WriteAllTextAsync(FullPath, json);
                Log(LogLevel.Debug, $"Skill point shop state saved to '{FullPath}'.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to save: {ex.Message}");
            }
        }

        public async Task LoadAsync()
        {
            if (_button == null) { Log(LogLevel.Error, "SkillPointShopButton not assigned."); return; }
            if (!File.Exists(FullPath)) { Log(LogLevel.Debug, "No save found — starting at tier 0."); return; }

            try
            {
                string json = await File.ReadAllTextAsync(FullPath);
                var data = JsonUtility.FromJson<SkillPointShopSaveData>(json);
                _button.RestoreState(data.GlobalTierIndex);
                Log(LogLevel.Debug, $"State restored: tier {data.GlobalTierIndex}.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to load: {ex.Message}");
            }
        }

        public void DeleteSave()
        {
            if (!File.Exists(FullPath)) return;
            try
            {
                File.Delete(FullPath);
                Log(LogLevel.Debug, "Skill point shop save deleted.");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Failed to delete save: {ex.Message}");
            }
        }

        private void Log(LogLevel level, string msg) =>
            LoggerService.PrintLogMessage(level, $"[SkillPointShopSaveSystem:{gameObject.name}] {msg}", LogCategory.Inventory);
    }
}
