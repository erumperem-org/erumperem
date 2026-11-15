using UnityEngine;

namespace Core.Rewards.UI
{
    /// <summary>
    /// Put this on the reward panel. Every time the panel is enabled it reads
    /// the corruption value and runs the reward pipeline (roll, deliver, save, UI).
    /// </summary>
    public sealed class RewardPanel : MonoBehaviour
    {
        [SerializeField] private CorruptionRewardFlow _flow;

        [Tooltip("Any component implementing ICorruptionProvider (e.g. DiskCorruptionProvider).")]
        [SerializeField] private MonoBehaviour _corruptionProvider;

        private int _runId;

        private async void OnEnable()
        {
            var provider = _corruptionProvider as ICorruptionProvider;
            if (_flow == null || provider == null)
            {
                Debug.LogError("[RewardPanel] Flow or corruption provider not assigned (or provider doesn't implement ICorruptionProvider).", this);
                return;
            }

            int myRun = ++_runId;
            double corruption = await provider.GetCorruptionAsync();

            // Panel was closed (or reopened) while reading: don't grant twice / while hidden.
            if (this == null || !isActiveAndEnabled || myRun != _runId) return;

            _flow.Run(corruption);
        }

        private void OnDisable() => _runId++;
    }
}
