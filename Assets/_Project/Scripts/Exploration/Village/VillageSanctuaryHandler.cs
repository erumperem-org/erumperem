using Services.DebugUtilities;
using UnityEngine;

/// <summary>
/// Ao entrar na vila: zera corrupção, cura Main e Companion e persiste o save de exploração.
/// </summary>
public sealed class VillageSanctuaryHandler : MonoBehaviour
{
    [SerializeField] private PlayableCharactersManager _playableCharactersManager;
    [SerializeField] private ExplorationCorruptionSystem _corruptionSystem;

    private void Awake()
    {
        if (_playableCharactersManager == null)
        {
            _playableCharactersManager = FindFirstObjectByType<PlayableCharactersManager>();
        }

        if (_corruptionSystem == null)
        {
            _corruptionSystem = FindFirstObjectByType<ExplorationCorruptionSystem>();
        }
    }

    private void OnEnable()
    {
        ExplorationVillageEvents.OnPlayerEnteredVillage += HandlePlayerEnteredVillage;
    }

    private void OnDisable()
    {
        ExplorationVillageEvents.OnPlayerEnteredVillage -= HandlePlayerEnteredVillage;
    }

    private void HandlePlayerEnteredVillage()
    {
        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[HEAL-DEBUG] [VILLAGE] HandlePlayerEnteredVillage: iniciando reset de santuário (cura total + corrupção 0).",
            LogCategory.Player);

        if (_corruptionSystem != null)
        {
            float corruptionBeforeReset = _corruptionSystem.Corruption;
            _corruptionSystem.Corruption = 0f;
            _corruptionSystem.SaveState();

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[HEAL-DEBUG] [VILLAGE] Corrupção zerada {corruptionBeforeReset:F1} → {_corruptionSystem.Corruption:F1}.",
                LogCategory.Player);
        }

        HealPlayableIfPresent(_playableCharactersManager?.Main);
        HealPlayableIfPresent(_playableCharactersManager?.Companion);

        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext != null)
        {
            loadContext.SaveState();
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[HEAL-DEBUG] [VILLAGE] Santuário aplicado: corrupção zerada e party curada.",
            LogCategory.Player);
    }

    private static void HealPlayableIfPresent(IPlayableCharacter playableCharacter)
    {
        if (playableCharacter is not PlayableCharacter concretePlayableCharacter)
        {
            return;
        }

        var healthBar = concretePlayableCharacter.HealthBar;
        if (healthBar == null)
        {
            return;
        }

        float healthBeforeHeal = healthBar.CurrentHealth;
        healthBar.HealFull();

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[HEAL-DEBUG] [VILLAGE] '{concretePlayableCharacter.CharacterName}' curado " +
            $"{healthBeforeHeal} → {healthBar.CurrentHealth}/{healthBar.MaxHealth}.",
            LogCategory.Player);
    }
}
