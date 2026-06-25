namespace Game.Core.Diagnostics;

/// <summary>
/// Rastreio opcional de cura/corrupção no motor (Game.Core).
/// A camada Unity regista <see cref="OnLog"/> para espelhar no Console.
/// </summary>
public static class HealDebugTrace
{
    public static Action<string>? OnLog;

    internal static void Log(string message)
    {
        var line = "[HEAL-DEBUG] " + message;
        OnLog?.Invoke(line);
        System.Diagnostics.Debug.WriteLine(line);
    }
}
