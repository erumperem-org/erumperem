using UnityEngine;

public class NpcEnemyView : MonoBehaviour
{
    public ExplorationCorruptionSystem corruptionSystem;
    public GameObject low, mid, high;

    void Awake()
    {
        corruptionSystem = FindFirstObjectByType<ExplorationCorruptionSystem>();
        SetEnemyView(corruptionSystem != null ? corruptionSystem.CurrentTier : CorruptionTier.Low);
    }

    void Start()
    {
        if (corruptionSystem == null)
        {
            return;
        }

        corruptionSystem.OnTierLow += SetEnemyAsLow;
        corruptionSystem.OnTierMid += SetEnemyAsMid;
        corruptionSystem.OnTierHigh += SetEnemyAsHigh;
    }

    void OnDestroy()
    {
        if (corruptionSystem == null)
        {
            return;
        }

        corruptionSystem.OnTierLow -= SetEnemyAsLow;
        corruptionSystem.OnTierMid -= SetEnemyAsMid;
        corruptionSystem.OnTierHigh -= SetEnemyAsHigh;
    }

    void SetEnemyAsLow() => SetEnemyView(CorruptionTier.Low);
    void SetEnemyAsMid() => SetEnemyView(CorruptionTier.Mid);
    void SetEnemyAsHigh() => SetEnemyView(CorruptionTier.High);
    void SetEnemyView(CorruptionTier tier)
    {
        if (low != null)
        {
            low.SetActive(tier == CorruptionTier.Low);
        }

        if (mid != null)
        {
            mid.SetActive(tier == CorruptionTier.Mid);
        }

        if (high != null)
        {
            high.SetActive(tier == CorruptionTier.High);
        }
    }
}
