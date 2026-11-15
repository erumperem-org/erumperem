using UnityEngine;

namespace Core.Rewards
{
    /// <summary>
    /// Glue: corruption value → roll (CorruptionRewardDispenser) → deliver +
    /// save + UI (RewardGrantor). Replaces GenerateRewardsForCombatExit of the old system.
    /// </summary>
    public sealed class CorruptionRewardFlow : MonoBehaviour
    {
        [SerializeField] private CorruptionRewardDispenser _dispenser;
        [SerializeField] private RewardGrantor _grantor;

        public void Run(double corruptionValue)
        {
            if (_dispenser == null || _grantor == null)
            {
                Debug.LogError("[CorruptionRewardFlow] Dispenser or Grantor not assigned.", this);
                return;
            }

            _grantor.Grant(_dispenser.GenerateFromCorruption(corruptionValue));
        }
    }
}
