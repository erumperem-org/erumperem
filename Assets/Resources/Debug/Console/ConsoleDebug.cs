using UnityEngine;

namespace Services.DebugUtilities.Console
{
    public static class LoggerService
    {
        /// <summary>
        /// Logs a message with a given level and category.
        /// </summary>
        public static void PrintLogMessage(LogLevel logLevel, LogCategory logCategory, string message)
        {
            string colorHex = ColorUtility.ToHtmlStringRGB(logCategory.Color);
            string formattedMessage = $"<color=#{colorHex}>[{logCategory.Name.ToUpper()}]</color> {message}";
            Dispatch(logLevel, formattedMessage);
        }

        /// <summary>
        /// Logs a message with a given level, category, and success status.
        /// </summary>
        public static void PrintLogMessage(LogLevel logLevel, LogCategory logCategory, bool success, string message)
        {
            string categoryHex = ColorUtility.ToHtmlStringRGB(logCategory.Color);
            Color statusColor = success ? Color.green : Color.red;
            string statusHex = ColorUtility.ToHtmlStringRGB(statusColor);
            string statusLabel = success ? "[SUCCESSFUL]" : "[FAILURE]";

            string formattedMessage =
                $"<color=#{categoryHex}>[{logCategory.Name.ToUpper()}]</color> " +
                $"<color=#{statusHex}>{statusLabel}</color> " +
                $"{message}";

            Dispatch(logLevel, formattedMessage);
        }

        private static void Dispatch(LogLevel logLevel, string message)
        {
            switch (logLevel)
            {
                case LogLevel.Warning:
                    Debug.LogWarning(message);
                    break;
                case LogLevel.Error:
                    Debug.LogError(message);
                    break;
                default: // LogLevel.Debug
                    Debug.Log(message);
                    break;
            }
        }
    }
}