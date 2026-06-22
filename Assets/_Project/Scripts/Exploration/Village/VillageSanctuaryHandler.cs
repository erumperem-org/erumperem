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
        if (_corruptionSystem != null)
        {
            _corruptionSystem.Corruption = 0f;
            _corruptionSystem.SaveState();
        }

        HealPlayableIfPresent(_playableCharactersManager?.Main);
        HealPlayableIfPresent(_playableCharactersManager?.Companion);

        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext != null)
        {
            loadContext.SaveState();
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[VILLAGE] Santuário: corrupção zerada e party curada.",
            LogCategory.Player);
    }

    private static void HealPlayableIfPresent(IPlayableCharacter playableCharacter)
    {
        if (playableCharacter is not PlayableCharacter concretePlayableCharacter)
        {
            return;
        }

        concretePlayableCharacter.HealthBar?.HealFull();
    }
}
