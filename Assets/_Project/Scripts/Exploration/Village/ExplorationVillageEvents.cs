using System;

/// <summary>
/// Hub estático para entrada e saída da zona da vila (santuário).
/// </summary>
public static class ExplorationVillageEvents
{
    public static bool IsPlayerInsideVillage { get; private set; }

    public static event Action OnPlayerEnteredVillage;
    public static event Action OnPlayerExitedVillage;

    internal static void RaisePlayerEnteredVillage()
    {
        IsPlayerInsideVillage = true;
        OnPlayerEnteredVillage?.Invoke();
    }

    internal static void RaisePlayerExitedVillage()
    {
        IsPlayerInsideVillage = false;
        OnPlayerExitedVillage?.Invoke();
    }
}
