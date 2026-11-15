using System.Threading.Tasks;
using BarSystem.Config;
using BarSystem.Persistence;
using Services.DebugUtilities;
using UnityEngine;

namespace Core.Rewards
{
    /// <summary>
    /// Supplies the corruption value from the Corruption Bar's save, read through
    /// the bar's own repository (so it follows the selected save slot).
    /// Falls back to the config's CurrentDefault, exactly like CorruptionBarInstaller does.
    /// </summary>
    public sealed class BarCorruptionProvider : MonoBehaviour, ICorruptionProvider
    {
        [Tooltip("The SAME BarConfigSO used by CorruptionBarInstaller (its Id is the save key).")]
        [SerializeField] private BarConfigSO _corruptionConfig;

        private IBarStateRepository _repository;

        public Task<double> GetCorruptionAsync()
        {
            if (_corruptionConfig == null)
            {
                LoggerService.PrintLogMessage(LogLevel.Error,
                    "[BarCorruptionProvider] BarConfigSO not assigned — using 0.",
                    LogCategory.Inventory);
                return Task.FromResult(0.0);
            }

            _repository ??= new JsonFileBarStateRepository();

            var saved = _repository.Load(_corruptionConfig.Id);
            double corruption = saved != null ? saved.Current : _corruptionConfig.CurrentDefault;

            return Task.FromResult(corruption);
        }
    }
}