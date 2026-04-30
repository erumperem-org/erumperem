using System;
using System.Collections.Generic;
using Core.Tokens;
using UnityEngine;
using Services.DebugUtilities.Canvas;
using Services.DebugUtilities;
using Services.DebugUtilities.Console;
using Unity.VisualScripting.Antlr3.Runtime;

public class ApplyingTokenTest : MonoBehaviour
{
    private TokenContainerController Current;
    public TokenContainerController CurrentA;
    public TokenContainerController CurrentB;
    public TokenContainerController CurrentC;

    public void AllocBurnToken()      => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new BurnToken(applyDamage)));
    public void AllocBlightToken()    => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new BlightToken(applyDamage, applyHealDrain)));
    public void AllocBleedToken()     => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new BleedToken(applyDamage)));
    public void AllocBlockToken()     => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new BlockToken()));
    public void AllocBlockPlusToken() => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new BlockPlusToken()));
    public void AllocDodgeToken()     => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new DodgeToken(applyEvasion)));
    public void AllocBlindToken()     => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new BlindToken(Current, applyMissChance)));
    public void AllocTauntToken()     => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new TauntToken(Current, onTauntApplied)));
    public void AllocStealthToken()   => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new StealthToken(Current, setUntargetable)));
    public void AllocComboToken()     => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new ComboToken()));
    public void AllocStunToken()      => _ = TokenContainerController.AddTokenToContainer(new TokenAllocationContext("Player", Current, new StunToken(Current, setActingBlocked)));

    private static readonly HashSet<LogCategory> CombatCategory = new() { LogCategory.Combat };

    private void applyDamage(float value) =>
        CanvasLoggerService.PrintLogMessage(LogLevel.Debug, CombatCategory, value > 0,
            $"Damage applied — {value:F1} hp removed from {Current.name}");

    private void applyEvasion(float value) =>
        CanvasLoggerService.PrintLogMessage(LogLevel.Debug, CombatCategory, value > 0,
            $"Evasion set — {value * 100f:F1}% dodge chance on {Current.name}");

    private void applyHealDrain(float value) =>
        CanvasLoggerService.PrintLogMessage(LogLevel.Debug, CombatCategory, value > 0,
            $"Heal drain applied — {value:F1} healing blocked from {Current.name}");

    private void applyMissChance(float value) =>
        CanvasLoggerService.PrintLogMessage(LogLevel.Debug, CombatCategory, value > 0,
            $"Miss chance set — {value * 100f:F0}% on {Current.name}");

    private void setActingBlocked(bool blocked) =>
        CanvasLoggerService.PrintLogMessage(LogLevel.Debug, CombatCategory, blocked,
            $"{Current.name} acting blocked: {blocked}");

    private void setUntargetable(bool untargetable) =>
        CanvasLoggerService.PrintLogMessage(LogLevel.Debug, CombatCategory, untargetable,
            $"{Current.name} untargetable: {untargetable}");

    private void onTauntApplied() =>
        CanvasLoggerService.PrintLogMessage(LogLevel.Debug, CombatCategory, true,
            $"Taunt applied — all enemies must target {Current.name}");

    public void SetCurrentAsA() => Current = CurrentA;
    public void SetCurrentAsB() => Current = CurrentB;
    public void SetCurrentAsC() => Current = CurrentC;
    public void Tick()       => Current.Tick();
    public void ExecuteAll() => Current.ExecuteAll();
    public void RemoveAll()  => Current.RemoveAll();
}