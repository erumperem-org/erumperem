using UnityEngine;

public class NpcEnemyView : MonoBehaviour
{
    public ExplorationCorruptionSystem corruptionSystem;
    public GameObject low, mid, high;

    void Awake()
    {
        corruptionSystem = FindFirstObjectByType<ExplorationCorruptionSystem>();
        SetEnemyView(corruptionSystem.CurrentTier);
    }
    void Start()
    {
        corruptionSystem.OnTierLow += SetEnemyAsLow;
        corruptionSystem.OnTierMid += SetEnemyAsMid;
        corruptionSystem.OnTierHigh += SetEnemyAsHigh;
    }

    void SetEnemyAsLow() => SetEnemyView(CorruptionTier.Low);
    void SetEnemyAsMid() => SetEnemyView(CorruptionTier.Mid);
    void SetEnemyAsHigh() => SetEnemyView(CorruptionTier.High);
    void SetEnemyView(CorruptionTier tier)
    {
        switch (tier)
        {
            case CorruptionTier.Low:
                low.SetActive(true);
                mid.SetActive(false);
                high.SetActive(false);
                break;
            case CorruptionTier.Mid:
                low.SetActive(false);
                mid.SetActive(true);
                high.SetActive(false);
                break;
            case CorruptionTier.High:
                low.SetActive(false);
                mid.SetActive(false);
                high.SetActive(true);
                break;
        }
    }
}
