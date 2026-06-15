using UnityEngine;

public class NpcEnemyView : MonoBehaviour
{
    [SerializeField] private ExplorationCorruptionSystem corruptionSystem;
    [SerializeField] private GameObject lowTierVisual;
    [SerializeField] private GameObject midTierVisual;
    [SerializeField] private GameObject highTierVisual;

    private bool _isSubscribedToCorruptionEvents;

    private void Awake()
    {
        TryResolveCorruptionSystem();
        ApplyCorruptionTierVisuals(ResolveCurrentCorruptionTier());
    }

    private void OnEnable()
    {
        TryResolveCorruptionSystem();
        TrySubscribeToCorruptionEvents();
        ApplyCorruptionTierVisuals(ResolveCurrentCorruptionTier());
    }

    private void OnDisable()
    {
        UnsubscribeFromCorruptionEvents();
    }

    private void OnDestroy()
    {
        UnsubscribeFromCorruptionEvents();
    }

    /// <summary>Chamado ao sair da pool / spawnar no mapa.</summary>
    public void RefreshCorruptionTierVisuals()
    {
        TryResolveCorruptionSystem();
        ApplyCorruptionTierVisuals(ResolveCurrentCorruptionTier());
    }

    private void TryResolveCorruptionSystem()
    {
        if (corruptionSystem != null)
        {
            return;
        }

        corruptionSystem = FindFirstObjectByType<ExplorationCorruptionSystem>();
    }

    private CorruptionTier ResolveCurrentCorruptionTier()
    {
        return corruptionSystem != null
            ? corruptionSystem.CurrentTier
            : CorruptionTier.Low;
    }

    private void TrySubscribeToCorruptionEvents()
    {
        if (_isSubscribedToCorruptionEvents || corruptionSystem == null)
        {
            return;
        }

        corruptionSystem.OnTierLow += HandleCorruptionTierLow;
        corruptionSystem.OnTierMid += HandleCorruptionTierMid;
        corruptionSystem.OnTierHigh += HandleCorruptionTierHigh;
        _isSubscribedToCorruptionEvents = true;
    }

    private void UnsubscribeFromCorruptionEvents()
    {
        if (!_isSubscribedToCorruptionEvents || corruptionSystem == null)
        {
            return;
        }

        corruptionSystem.OnTierLow -= HandleCorruptionTierLow;
        corruptionSystem.OnTierMid -= HandleCorruptionTierMid;
        corruptionSystem.OnTierHigh -= HandleCorruptionTierHigh;
        _isSubscribedToCorruptionEvents = false;
    }

    private void HandleCorruptionTierLow() => ApplyCorruptionTierVisuals(CorruptionTier.Low);
    private void HandleCorruptionTierMid() => ApplyCorruptionTierVisuals(CorruptionTier.Mid);
    private void HandleCorruptionTierHigh() => ApplyCorruptionTierVisuals(CorruptionTier.High);

    private void ApplyCorruptionTierVisuals(CorruptionTier tier)
    {
        if (lowTierVisual != null)
        {
            lowTierVisual.SetActive(tier == CorruptionTier.Low);
        }

        if (midTierVisual != null)
        {
            midTierVisual.SetActive(tier == CorruptionTier.Mid);
        }

        if (highTierVisual != null)
        {
            highTierVisual.SetActive(tier == CorruptionTier.High);
        }
    }
}
