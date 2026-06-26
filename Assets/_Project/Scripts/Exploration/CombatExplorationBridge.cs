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
    private const float CombatReentryBlockSeconds = 5f;
    private const float PostCombatMonsterSpawnBlockSeconds = 5f;
    private const float ExplorationSceneCombatContactActivationDelaySeconds = 5f;
    private const float VictoryReturnSeparationFromCombatEntry = 6f;
    public static CombatExplorationBridge Instance { get; private set; }

    public static bool IsCombatReentryBlocked =>
        Instance != null && Time.time < Instance._combatReentryBlockedUntil;

    /// <summary>Bloqueia spawn de inimigos no overworld durante alguns segundos após o combate.</summary>
    public static bool IsMonsterSpawnBlocked =>
        Instance != null && Time.time < Instance._monsterSpawnBlockedUntil;

    /// <summary>Bloqueia contatos de combate logo após carregar o overworld.</summary>
    public static bool AreExplorationCombatContactsBlocked =>
        Instance != null && Time.time < Instance._explorationCombatContactsBlockedUntil;

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
    private float _monsterSpawnBlockedUntil;
    private float _explorationCombatContactsBlockedUntil;
    private bool _hasLastCombatEntryPosition;
    private Vector3 _lastCombatEntryWorldPosition;
    private IReadOnlyList<string> _pendingCombatAllyCharacterNames;

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

    /// <summary>Party [Main, Companion] capturada ao entrar em combate (autoritativa na cena de combate).</summary>
    public IReadOnlyList<string> TryGetPendingCombatAllyCharacterNames() => _pendingCombatAllyCharacterNames;

    public void BlockExplorationCombatContactsAfterSceneLoad()
    {
        _explorationCombatContactsBlockedUntil = Mathf.Max(
            _explorationCombatContactsBlockedUntil,
            Time.time + ExplorationSceneCombatContactActivationDelaySeconds);

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[COMBAT-BRIDGE] Contatos de combate bloqueados por " +
            $"{ExplorationSceneCombatContactActivationDelaySeconds:F1}s após load do overworld.",
            LogCategory.Player);
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
            _pendingCombatAllyCharacterNames = CombatPartyResolver.NormalizeCombatParty(null);
            return;
        }

        ExplorationLoadContext.Instance.SaveState();
        ExplorationLoadContext.Instance.RememberExplorationStateAtCombatEntry();
        var partyFromSnapshots = ExplorationLoadContext.Instance.GetCombatAllyCharacterNamesFromSnapshots();
        _pendingCombatAllyCharacterNames = CombatPartyResolver.NormalizeCombatParty(partyFromSnapshots);

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[COMBAT-BRIDGE] Party de combate: {string.Join(", ", _pendingCombatAllyCharacterNames)}.",
            LogCategory.Player);

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
    /// Não cura — copia valores actuais do save de exploração.
    /// </summary>
    public void SeedBattleFromExploration(BattleState battleState)
    {
        if (battleState == null)
        {
            return;
        }

        var loadContext = ExplorationLoadContext.EnsureRuntimeInstance();
        Debug.Log("[Save] SeedBattleFromExploration: a aplicar HP/corrupção do save aos aliados.");

        var savedAllyHealthByCharacter = loadContext.GetSavedHealthByCharacterName();
        if (savedAllyHealthByCharacter.Count == 0)
        {
            Debug.LogWarning(
                "[Save] SeedBattleFromExploration: nenhum snapshot em memória — " +
                "verifica se exploration_save.json existe e foi carregado.");
        }
        var allies = battleState.Allies;
        var combatAllyCharacterNames = CombatPartyResolver.GetCombatAllyCharacterNames();
        for (int allyIndex = 0; allyIndex < allies.Count && allyIndex < combatAllyCharacterNames.Count; allyIndex++)
        {
            var characterName = combatAllyCharacterNames[allyIndex];
            if (!savedAllyHealthByCharacter.TryGetValue(characterName, out var allyHealth))
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    $"[COMBAT-BRIDGE] Snapshot de HP ausente para '{characterName}'.",
                    LogCategory.Player);
                continue;
            }

            if (allyHealth.MaxHealth <= 0f)
            {
                LoggerService.PrintLogMessage(LogLevel.Warning,
                    $"[COMBAT-BRIDGE] HP máximo inválido para '{characterName}'.",
                    LogCategory.Player);
                continue;
            }

            var ally = allies[allyIndex];
            var hitPointsBeforeSeed = ally.Health.CurrentHp;
            var maxHitPointsBeforeSeed = ally.Health.MaxHp;

            int maxHitPoints = Mathf.RoundToInt(allyHealth.MaxHealth);
            int currentHitPoints = Mathf.Clamp(Mathf.RoundToInt(allyHealth.CurrentHealth), 0, maxHitPoints);

            ally.Health = new HealthComponent
            {
                MaxHp = maxHitPoints,
                CurrentHp = currentHitPoints,
                IsDead = currentHitPoints <= 0,
                IsDeathblowPending = false,
            };

            Debug.Log(
                $"[Save] Combate seed '{characterName}': save CurrentHealth={allyHealth.CurrentHealth:F1} " +
                $"→ CurrentHp={currentHitPoints}/{maxHitPoints} " +
                $"(antes do seed: {hitPointsBeforeSeed}/{maxHitPointsBeforeSeed}).");

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[HEAL-DEBUG] [COMBAT-SEED] '{characterName}' HP seed " +
                $"{hitPointsBeforeSeed}/{maxHitPointsBeforeSeed} → {currentHitPoints}/{maxHitPoints}.",
                LogCategory.Player);
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

    /// <summary>Regista o resultado do combate, persiste HP/corrupção e bloqueia spawn temporário.</summary>
    public void NotifyCombatEnded(BattleState battleState, bool alliesWon)
    {
        _hasPendingCombatReturn = true;
        _lastBattleAlliesWon = alliesWon;
        _lastBattleState = battleState;
        _combatReentryBlockedUntil = Time.time + CombatReentryBlockSeconds;
        _monsterSpawnBlockedUntil = Time.time + PostCombatMonsterSpawnBlockSeconds;

        var loadContext = ExplorationLoadContext.Instance;
        if (loadContext == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[COMBAT-BRIDGE] ExplorationLoadContext ausente — HP/corrupção pós-combate não persistidos.",
                LogCategory.Player);
            return;
        }

        loadContext.ApplyCombatHealthAndCorruptionToSnapshots(
            battleState,
            CombatPartyResolver.GetCombatAllyCharacterNames(),
            ResolvePostCombatCorruptionValue(battleState),
            persistToDisk: true);

        LoggerService.PrintLogMessage(LogLevel.Debug,
            "[COMBAT-BRIDGE] HP e corrupção pós-combate persistidos.",
            LogCategory.Player);
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
            loadContext.ApplyCombatDefeatReturnToVillage();
            _requiresCombatEntryZoneClearance = false;
        }

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

        var combatAllyCharacterNames = CombatPartyResolver.GetCombatAllyCharacterNames();
        var mainCharacterName = combatAllyCharacterNames.Count > 0
            ? combatAllyCharacterNames[0]
            : null;

        if (!string.IsNullOrWhiteSpace(mainCharacterName) &&
            loadContext.TryGetSavedPositionForCharacter(mainCharacterName, out var entryPosition))
        {
            Instance._lastCombatEntryWorldPosition = entryPosition;
            Instance._hasLastCombatEntryPosition = true;
            return;
        }

        Instance._hasLastCombatEntryPosition = false;
    }

    private static double ResolvePostCombatCorruptionValue(BattleState battleState)
    {
        if (CorruptionManager.Instance != null)
        {
            return CorruptionManager.Instance.GetCorruptionValue();
        }

        return battleState?.CorruptionValue ?? 0d;
    }

    private void ClearPendingReturn()
    {
        _hasPendingCombatReturn = false;
        _lastBattleState = null;
        _pendingCombatAllyCharacterNames = null;
    }
}