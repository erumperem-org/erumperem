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
    private static readonly string[] CombatAllyCharacterNames = { "Wulfric", "Girl" };

    public static CombatExplorationBridge Instance { get; private set; }

    private bool _hasPendingCombatReturn;
    private bool _lastBattleAlliesWon;
    private BattleState _lastBattleState;

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

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[COMBAT-BRIDGE] Estado de exploração salvo antes do combate.",
            LogCategory.Player);
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

        loadContext.ApplyCombatOutcomeToSnapshots(
            _lastBattleState,
            CombatAllyCharacterNames,
            returnToVillage: !_lastBattleAlliesWon);

        ClearPendingReturn();
        ScenesManager.Instance.LoadSceneByName(targetSceneName);
        return true;
    }

    private void ClearPendingReturn()
    {
        _hasPendingCombatReturn = false;
        _lastBattleState = null;
    }
}
