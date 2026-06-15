using System;
using System.Collections.Generic;
using Erumperem.Combat;
using Game.Core.Domain;
using Game.Core.Models;
using Services.DebugUtilities;
using UnityEngine;

/// <summary>
/// Coordena save/load de exploração com entradas e saídas de combate:
/// posição, HP dos heróis e corrupção.
/// </summary>
public sealed class CombatExplorationBridge : MonoBehaviour
{
    private static readonly string[] CombatAllyCharacterNames = { "Wulfric", "Matsuda" };

    private const float CombatReentryBlockSeconds = 5f;
    private const float VictoryReturnSeparationFromCombatEntry = 6f;
    private const string MainExplorationCharacterName = "Wulfric";

    public static CombatExplorationBridge Instance { get; private set; }

    public static bool IsCombatReentryBlocked =>
        Instance != null && Time.time < Instance._combatReentryBlockedUntil;

    /// <summary>
    /// Após combate iniciado por contacto estático, bloqueia reentrada até o jogador sair da zona.
    /// Persiste entre reloads de cena (DontDestroyOnLoad).
    /// </summary>
    public static bool RequiresCombatEntryZoneClearance =>
        Instance != null && Instance._requiresCombatEntryZoneClearance;

    private bool _hasPendingCombatReturn;
    private bool _lastBattleAlliesWon;
    private bool _lastCombatWasFromStaticContact;
    private bool _requiresCombatEntryZoneClearance;
    private BattleState _lastBattleState;
    private float _combatReentryBlockedUntil;
    private bool _hasLastCombatEntryPosition;
    private Vector3 _lastCombatEntryWorldPosition;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>Chamado imediatamente antes de carregar a cena de combate.</summary>
    public void NotifyEnteringCombat()
    {
        _hasPendingCombatReturn = false;
        _lastBattleState = null;

        if (ExplorationLoadContext.Instance == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[COMBAT-BRIDGE] ExplorationLoadContext ausente — estado de exploração não salvo.",
                LogCategory.Player);
            return;
        }

        ExplorationLoadContext.Instance.SaveState();
        RememberCombatEntryPosition(ExplorationLoadContext.Instance);

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[COMBAT-BRIDGE] Estado de exploração salvo antes do combate.",
            LogCategory.Player);
    }

    /// <summary>Marca que o combate veio de um inimigo estático — exige sair da zona antes de reentrar.</summary>
    public void NotifyStaticCombatContactTriggered()
    {
        _lastCombatWasFromStaticContact = true;
        _requiresCombatEntryZoneClearance = true;
    }

    /// <summary>Chamado quando o jogador deixa a zona de contacto do inimigo estático.</summary>
    public void NotifyPlayerLeftCombatEntryZone()
    {
        _requiresCombatEntryZoneClearance = false;
    }

    /// <summary>
    /// Aplica HP e corrupção da exploração ao estado de combate recém-criado.
    /// </summary>
    public void SeedBattleFromExploration(BattleState battleState)
    {
        if (battleState == null)
        {
            return;
        }

        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext == null)
        {
            return;
        }

        var explorationHealthByCharacter = loadContext.GetSavedHealthByCharacterName();
        var allies = battleState.Allies;
        for (int allyIndex = 0; allyIndex < allies.Count && allyIndex < CombatAllyCharacterNames.Length; allyIndex++)
        {
            var characterName = CombatAllyCharacterNames[allyIndex];
            if (!explorationHealthByCharacter.TryGetValue(characterName, out var healthSnapshot))
            {
                continue;
            }

            if (healthSnapshot.MaxHealth <= 0f)
            {
                continue;
            }

            var ally = allies[allyIndex];
            var maxHitPoints = Math.Max(1, Mathf.RoundToInt(healthSnapshot.MaxHealth));
            var currentHitPoints = Mathf.Clamp(
                Mathf.RoundToInt(healthSnapshot.CurrentHealth),
                0,
                maxHitPoints);

            ally.Health = new HealthComponent
            {
                MaxHp = maxHitPoints,
                CurrentHp = currentHitPoints,
                IsDead = currentHitPoints <= 0,
                IsDeathblowPending = false,
            };
        }

        var explorationCorruption = loadContext.GetSavedCorruptionValue();
        battleState.CorruptionValue = Math.Max(0, explorationCorruption);

        if (CorruptionManager.Instance != null)
        {
            CorruptionManager.Instance.SetCorruptionValue(battleState.CorruptionValue);
        }

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[COMBAT-BRIDGE] Combate inicializado com corrupção {battleState.CorruptionValue:F1}.",
            LogCategory.Player);
    }

    /// <summary>Regista o resultado do combate para o retorno à exploração.</summary>
    public void NotifyCombatEnded(BattleState battleState, bool alliesWon)
    {
        _hasPendingCombatReturn = true;
        _lastBattleAlliesWon = alliesWon;
        _lastBattleState = battleState;
    }

    /// <summary>
    /// Prepara o save de exploração e carrega Overworld.
    /// Retorna <c>true</c> se tratou o retorno pós-combate.
    /// </summary>
    public bool TryCompleteReturnToExploration(string targetSceneName)
    {
        if (!_hasPendingCombatReturn || _lastBattleState == null)
        {
            return false;
        }

        if (!string.Equals(targetSceneName, "Overworld", StringComparison.Ordinal))
        {
            return false;
        }

        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[COMBAT-BRIDGE] ExplorationLoadContext ausente — retorno sem restaurar estado.",
                LogCategory.Player);
            ClearPendingReturn();
            ScenesManager.Instance.LoadSceneByName(targetSceneName);
            return true;
        }

        var returnFromStaticSpawnContact = _lastCombatWasFromStaticContact;

        loadContext.ApplyCombatOutcomeToSnapshots(
            _lastBattleState,
            CombatAllyCharacterNames,
            returnToVillage: !_lastBattleAlliesWon && !returnFromStaticSpawnContact,
            persistToDisk: false);

        if (returnFromStaticSpawnContact)
        {
            loadContext.ReturnSnapshotsToResetSpawn();
            _requiresCombatEntryZoneClearance = false;
            _lastCombatWasFromStaticContact = false;
        }
        else if (_lastBattleAlliesWon && _hasLastCombatEntryPosition)
        {
            loadContext.NudgeSnapshotsAwayFromWorldPoint(
                _lastCombatEntryWorldPosition,
                VictoryReturnSeparationFromCombatEntry);
            _requiresCombatEntryZoneClearance = false;
        }
        else if (!_lastBattleAlliesWon)
        {
            _requiresCombatEntryZoneClearance = false;
        }

        _combatReentryBlockedUntil = Time.time + CombatReentryBlockSeconds;
        ClearPendingReturn();
        loadContext.FinishCombatReturnAndLoadExploration(targetSceneName);
        return true;
    }

    private static void RememberCombatEntryPosition(ExplorationLoadContext loadContext)
    {
        if (loadContext == null)
        {
            return;
        }

        if (loadContext.TryGetSavedPositionForCharacter(MainExplorationCharacterName, out var entryPosition))
        {
            Instance._lastCombatEntryWorldPosition = entryPosition;
            Instance._hasLastCombatEntryPosition = true;
            return;
        }

        Instance._hasLastCombatEntryPosition = false;
    }

    private void ClearPendingReturn()
    {
        _hasPendingCombatReturn = false;
        _lastBattleState = null;
    }
}
