using System;
using System.Reflection;
using Erumperem.Combat;
using Game.Core.Models;
using UnityEngine;

public static class CombatExistingTargetInputAdapter
{
    private const string RuntimeFieldName = "_runtime";
    private const string SelectedEnemyTargetFieldName = "SelectedEnemyTarget";
    private const string PlayerTargetSelectionFieldName = "_playerTargetSelection";
    private const string TryCastUiSelectedSkillOnTargetMethodName = "TryCastUiSelectedSkillOnTarget";

    private static readonly BindingFlags PrivateInstanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly BindingFlags PublicInstanceFlags = BindingFlags.Instance | BindingFlags.Public;

    public static bool TryPreviewTarget(CombatPrototypeController combatSession, Combatant target)
    {
        if (combatSession == null || target == null || target.Health.IsDead)
        {
            return false;
        }

        var runtimeField = typeof(CombatPrototypeController).GetField(RuntimeFieldName, PrivateInstanceFlags);

        if (runtimeField == null)
        {
            Debug.LogError($"CombatExistingTargetInputAdapter: campo privado '{RuntimeFieldName}' não foi encontrado em CombatPrototypeController.");
            return false;
        }

        var runtime = runtimeField.GetValue(combatSession);

        if (runtime == null)
        {
            Debug.LogError("CombatExistingTargetInputAdapter: runtime do combate não está disponível.");
            return false;
        }

        var selectedEnemyTargetField = runtime.GetType().GetField(SelectedEnemyTargetFieldName, PublicInstanceFlags);

        if (selectedEnemyTargetField == null)
        {
            Debug.LogError($"CombatExistingTargetInputAdapter: campo '{SelectedEnemyTargetFieldName}' não foi encontrado no runtime do combate.");
            return false;
        }

        selectedEnemyTargetField.SetValue(runtime, target);
        return true;
    }

    public static bool TryConfirmTarget(CombatPrototypeController combatSession, Combatant target)
    {
        if (combatSession == null || target == null || target.Health.IsDead)
        {
            return false;
        }

        var playerTargetSelectionField = typeof(CombatPrototypeController).GetField(PlayerTargetSelectionFieldName, PrivateInstanceFlags);

        if (playerTargetSelectionField == null)
        {
            Debug.LogError($"CombatExistingTargetInputAdapter: campo privado '{PlayerTargetSelectionFieldName}' não foi encontrado em CombatPrototypeController.");
            return false;
        }

        var playerTargetSelection = playerTargetSelectionField.GetValue(combatSession);

        if (playerTargetSelection == null)
        {
            Debug.LogError("CombatExistingTargetInputAdapter: sistema existente de seleção de alvo ainda não foi inicializado.");
            return false;
        }

        var tryCastMethod = playerTargetSelection.GetType().GetMethod(TryCastUiSelectedSkillOnTargetMethodName, PrivateInstanceFlags);

        if (tryCastMethod == null)
        {
            Debug.LogError($"CombatExistingTargetInputAdapter: método privado '{TryCastUiSelectedSkillOnTargetMethodName}' não foi encontrado.");
            return false;
        }

        try
        {
            var result = tryCastMethod.Invoke(playerTargetSelection, new object[] { target });
            return result is bool succeeded && succeeded;
        }
        catch (TargetInvocationException exception)
        {
            Debug.LogException(exception.InnerException ?? exception);
            return false;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }
}
