using System.Collections;
using Services.DebugUtilities;
using UnityEngine;

/// <summary>
/// Cura o Main e zera corrupção apenas se ele permanecer na vila por tempo suficiente.
/// </summary>
public sealed class VillageSanctuaryHandler : MonoBehaviour
{
    private const float RequiredMainStaySeconds = 3f;

    [SerializeField] private PlayableCharactersManager _playableCharactersManager;
    [SerializeField] private ExplorationCorruptionSystem _corruptionSystem;

    private Coroutine _pendingVillageHealCoroutine;
    private PlayableCharacter _pendingMainPlayableCharacter;

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
        ExplorationVillageEvents.OnPlayerExitedVillage += HandlePlayerExitedVillage;
    }

    private void OnDisable()
    {
        ExplorationVillageEvents.OnPlayerEnteredVillage -= HandlePlayerEnteredVillage;
        ExplorationVillageEvents.OnPlayerExitedVillage -= HandlePlayerExitedVillage;
        CancelPendingVillageHeal("handler desativado");
    }

    private void HandlePlayerEnteredVillage()
    {
        if (_pendingVillageHealCoroutine != null)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[HEAL-DEBUG] [VILLAGE] Main já aguarda cura do santuário; evento duplicado ignorado.",
                LogCategory.Player);
            return;
        }

        if (_playableCharactersManager?.Main is not PlayableCharacter mainPlayableCharacter)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[HEAL-DEBUG] [VILLAGE] Entrada na vila ignorada: Main não encontrado.",
                LogCategory.Player);
            return;
        }

        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext != null && loadContext.AreMainAndCompanionBelowOneHealth())
        {
            StartCoroutine(ResetSaveIfPartyWipedAtVillageOnEnterRoutine());
            return;
        }

        _pendingMainPlayableCharacter = mainPlayableCharacter;
        _pendingVillageHealCoroutine = StartCoroutine(ApplyVillageHealAfterRequiredStay(mainPlayableCharacter));

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[HEAL-DEBUG] [VILLAGE] Main '{mainPlayableCharacter.CharacterName}' entrou na vila; " +
            $"cura/corrupção aguardando {RequiredMainStaySeconds:F1}s.",
            LogCategory.Player);
    }

    private void HandlePlayerExitedVillage()
    {
        CancelPendingVillageHeal("Main saiu da vila antes do tempo");
    }

    private IEnumerator ResetSaveIfPartyWipedAtVillageOnEnterRoutine()
    {
        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext == null)
        {
            yield break;
        }

        var resetTask = loadContext.TryResetSaveAndApplyDefaultsIfMainAndCompanionAreDefeatedAsync();
        while (!resetTask.IsCompleted)
        {
            yield return null;
        }
    }

    private IEnumerator ApplyVillageHealAfterRequiredStay(PlayableCharacter mainPlayableCharacter)
    {
        yield return new WaitForSeconds(RequiredMainStaySeconds);

        _pendingVillageHealCoroutine = null;
        _pendingMainPlayableCharacter = null;

        if (!ExplorationVillageEvents.IsPlayerInsideVillage)
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[HEAL-DEBUG] [VILLAGE] Cura cancelada: Main não está mais dentro da vila.",
                LogCategory.Player);
            yield break;
        }

        if (mainPlayableCharacter == null ||
            !ReferenceEquals(_playableCharactersManager?.Main, mainPlayableCharacter))
        {
            LoggerService.PrintLogMessage(LogLevel.Debug,
                "[HEAL-DEBUG] [VILLAGE] Cura cancelada: Main mudou durante a espera.",
                LogCategory.Player);
            yield break;
        }

        ApplyVillageSanctuaryToMain(mainPlayableCharacter);
    }

    private void ApplyVillageSanctuaryToMain(PlayableCharacter mainPlayableCharacter)
    {
        if (_corruptionSystem != null)
        {
            float corruptionBeforeReset = _corruptionSystem.Corruption;
            _corruptionSystem.Corruption = 0f;
            _corruptionSystem.SaveState();

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[HEAL-DEBUG] [VILLAGE] Corrupção zerada {corruptionBeforeReset:F1} → {_corruptionSystem.Corruption:F1}.",
                LogCategory.Player);
        }

        HealMainIfPresent(mainPlayableCharacter);

        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext != null)
        {
            loadContext.SaveState();
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[HEAL-DEBUG] [VILLAGE] Santuário aplicado: corrupção zerada e Main curado.",
            LogCategory.Player);
    }

    private void CancelPendingVillageHeal(string reason)
    {
        if (_pendingVillageHealCoroutine == null)
        {
            return;
        }

        StopCoroutine(_pendingVillageHealCoroutine);
        _pendingVillageHealCoroutine = null;

        var pendingCharacterName = _pendingMainPlayableCharacter != null
            ? _pendingMainPlayableCharacter.CharacterName
            : "desconhecido";
        _pendingMainPlayableCharacter = null;

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[HEAL-DEBUG] [VILLAGE] Cura pendente de '{pendingCharacterName}' cancelada: {reason}.",
            LogCategory.Player);
    }

    private static void HealMainIfPresent(PlayableCharacter mainPlayableCharacter)
    {
        if (mainPlayableCharacter == null)
        {
            return;
        }

        var healthBar = mainPlayableCharacter.HealthBar;
        if (healthBar == null)
        {
            return;
        }

        float healthBeforeHeal = healthBar.CurrentHealth;
        healthBar.HealFullFromVillageSanctuary();

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[HEAL-DEBUG] [VILLAGE] Main '{mainPlayableCharacter.CharacterName}' curado " +
            $"{healthBeforeHeal} → {healthBar.CurrentHealth}/{healthBar.MaxHealth}.",
            LogCategory.Player);
    }
}
